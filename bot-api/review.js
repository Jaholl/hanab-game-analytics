import * as https from 'https';
import * as fs from 'fs';
import * as path from 'path';

import HGroup from './conventions/h-group.js';
import { ACTION, END_CONDITION, MAX_H_LEVEL, CLUE } from './constants.js';
import { getVariant, colourableSuits } from './variants.js';
import { State } from './basics/State.js';
import * as Utils from './tools/util.js';
import { globals } from './tools/util.js';
import { find_clues } from './conventions/h-group/clue-finder/clue-finder.js';
import { find_clue_value, determine_playable_card, select_play_clue } from './conventions/h-group/action-helper.js';
import { logCard, logClue, logPerformAction, logAction } from './tools/log.js';
import logger from './tools/logger.js';
import { HANABI_HOSTNAME } from './constants.js';
import { cardValue } from './basics/hanabi-util.js';

/**
 * @typedef {import('./types.js').Identity} Identity
 * @typedef {import('./basics/Action.ts').PerformAction} PerformAction
 * @typedef {import('./basics/Action.ts').Action} Action
 */

/**
 * Fetches a replay from hanab.live, given its id.
 * @param {string} id
 */
export function fetchReplay(id) {
	return new Promise((resolve, reject) => {
		const req = https.request(`https://${HANABI_HOSTNAME}/export/${id}`, (res) => {
			let raw_data = '';
			res.on('data', (chunk) => raw_data += chunk);
			res.on('end', () => {
				try {
					resolve(JSON.parse(raw_data));
				}
				catch (err) {
					reject(err);
				}
			});
		});
		req.on('error', (error) => reject(`Request error: ${error}`));
		req.end();
	});
}

/**
 * Compare two perform actions for equality.
 * @param {PerformAction} a
 * @param {PerformAction} b
 */
function actionsMatch(a, b) {
	if (a.type !== b.type) return false;
	if (a.target !== b.target) return false;
	if ((a.type === ACTION.RANK || a.type === ACTION.COLOUR) && a.value !== b.value) return false;
	return true;
}

/**
 * Describe a perform action in human-readable text.
 * @param {State} state
 * @param {PerformAction} action
 * @param {Identity[]} deck
 * @param {number} playerIndex
 */
function describeAction(state, action, deck, playerIndex) {
	const { type, target, value } = action;
	switch (type) {
		case ACTION.PLAY: {
			const card = deck[target];
			const slot = state.hands[playerIndex].indexOf(target) + 1;
			return `Play ${logCard(card)} (slot ${slot})`;
		}
		case ACTION.DISCARD: {
			const card = deck[target];
			const slot = state.hands[playerIndex].indexOf(target) + 1;
			return `Discard ${logCard(card)} (slot ${slot})`;
		}
		case ACTION.RANK:
			return `Clue ${value} to ${state.playerNames[target]}`;
		case ACTION.COLOUR: {
			const suits = colourableSuits(state.variant);
			return `Clue ${suits[value]?.toLowerCase() ?? value} to ${state.playerNames[target]}`;
		}
		case ACTION.END_GAME:
			return 'End game';
	}
}

/**
 * Cascade ranks mirror the bot's take_action() priority order.
 * Lower rank = checked earlier = higher priority.
 *
 *  1  Play into bluff (P0 with bluff status)
 *  2  Play into hidden finesse (P0 with hidden component)
 *  3  Urgent actions (unlock next, urgent saves/fixes)
 *  4  Generation discard
 *  5  Finesse clue involving next player
 *  6  Play finesse (P0)
 *  7  Sarcastic discard
 *  8  Play connecting / 5s (P1-P3)
 *  9  Discard known trash (high pace, low clues)
 * 10  Play clue (meets MCVP)
 * 11  Play any playable (P4-P5)
 * 12  Stall clues
 * 13  Discard known trash (any pace)
 * 14  Discard chop (default)
 */
const CASCADE = {
	BLUFF_PLAY:    1,
	HIDDEN_PLAY:   2,
	URGENT:        3,
	GEN_DISCARD:   4,
	FINESSE_CLUE:  5,
	PLAY_FINESSE:  6,
	SARCASTIC:     7,
	PLAY_MID:      8,
	TRASH_EARLY:   9,
	PLAY_CLUE:    10,
	PLAY_LOW:     11,
	STALL_CLUE:   12,
	TRASH_LATE:   13,
	DISCARD_CHOP: 14,
};

/**
 * Get the bot's recommendation and all candidates for a turn.
 * @param {import('./conventions/h-group.js').default} game
 * @param {Identity[]} deck
 */
async function analyzePosition(game) {
	const { state, me, common } = game;
	const nextPlayerIndex = state.nextPlayerIndex(state.ourPlayerIndex);

	// Find playable cards
	let playable_orders = me.thinksPlayables(state, state.ourPlayerIndex);
	let trash_orders = me.thinksTrash(state, state.ourPlayerIndex).filter(o => common.thoughts[o].saved);
	playable_orders = playable_orders.filter(o => !trash_orders.includes(o));

	const playable_priorities = determine_playable_card(game, playable_orders);

	// Find clues
	logger.off();
	let clue_data;
	try {
		clue_data = find_clues(game);
	}
	catch (e) {
		clue_data = { play_clues: [], save_clues: [], fix_clues: [], stall_clues: [] };
	}
	logger.on();

	// Extract clue value breakdown for display
	function clueBreakdown(result) {
		const { finesses, new_touched, playables, bad_touch, trash, cm_dupe, avoidable_dupe, elim, remainder } = result;
		const good_new_touched = new_touched.filter(c => !trash.includes(c.order));
		const new_touched_value = (good_new_touched.length >= 1) ? 0.51 + 0.1 * (good_new_touched.length - 1) : 0;
		const precision_value = good_new_touched.length > 0 ? Math.round(1000 / (good_new_touched.reduce((acc, c) => acc + c.inferred.length, 0) + 10)) / 1000 : 0;
		return {
			finesses: finesses.length,
			playables: playables.length,
			newTouched: good_new_touched.length,
			newTouchedValue: Math.round(new_touched_value * 100) / 100,
			badTouch: bad_touch.length,
			cmDupe: cm_dupe.length,
			avoidableDupe: typeof avoidable_dupe === 'number' ? avoidable_dupe : 0,
			elim,
			remainder: Math.round(remainder * 100) / 100,
			precision: Math.round(precision_value * 1000) / 1000,
			finesseDetails: finesses.map(f => {
				const card = state.deck[f.order] || {};
				return `${logCard(card)} (player ${f.playerIndex}, slot ${state.hands[f.playerIndex]?.indexOf(f.order) + 1})`;
			}),
			playableDetails: playables.map(o => logCard(state.deck[o])),
			badTouchDetails: bad_touch.map(o => logCard(state.deck[o]))
		};
	}

	// Build candidate list with cascade rank + clue value
	const candidates = [];

	// Add play clue candidates
	if (clue_data.play_clues) {
		for (const clues of clue_data.play_clues) {
			if (!Array.isArray(clues)) continue;
			for (const clue of clues) {
				try {
					const value = find_clue_value(clue.result);
					// Finesses involving next player get a higher cascade rank
					const involvesNext = clue.result.finesses?.length > 0 &&
						(clue.target === nextPlayerIndex || clue.result.finesses.some(f => f.playerIndex === nextPlayerIndex));
					const cascadeRank = involvesNext ? CASCADE.FINESSE_CLUE :
						(value >= 1 ? CASCADE.PLAY_CLUE : CASCADE.STALL_CLUE);
					candidates.push({
						action: Utils.clueToPerform(clue),
						value,
						cascadeRank,
						type: 'play_clue',
						description: logClue(clue),
						breakdown: clueBreakdown(clue.result)
					});
				}
				catch (e) { /* skip invalid clues */ }
			}
		}
	}

	// Add save clue candidates
	if (clue_data.save_clues) {
		for (const clue of clue_data.save_clues) {
			if (!clue) continue;
			try {
				const value = find_clue_value(clue.result);
				candidates.push({
					action: Utils.clueToPerform(clue),
					value,
					cascadeRank: CASCADE.URGENT,
					type: 'save_clue',
					description: logClue(clue),
					breakdown: clueBreakdown(clue.result)
				});
			}
			catch (e) { /* skip invalid clues */ }
		}
	}

	// Add stall clue candidates
	if (clue_data.stall_clues) {
		for (const stallGroup of clue_data.stall_clues) {
			if (!Array.isArray(stallGroup)) continue;
			for (const clue of stallGroup) {
				try {
					const value = find_clue_value(clue.result);
					candidates.push({
						action: Utils.clueToPerform(clue),
						value,
						cascadeRank: CASCADE.STALL_CLUE,
						type: 'stall_clue',
						description: logClue(clue),
						breakdown: clueBreakdown(clue.result)
					});
				}
				catch (e) { /* skip */ }
			}
		}
	}

	// Add playable card candidates with cascade rank based on play priority
	for (let priority = 0; priority < playable_priorities.length; priority++) {
		for (const order of playable_priorities[priority]) {
			const card = state.deck[order];
			const slot = state.hands[state.ourPlayerIndex].indexOf(order) + 1;

			let cascadeRank;
			if (priority === 0)      cascadeRank = CASCADE.PLAY_FINESSE;  // P0: finesse/blind play
			else if (priority <= 3)  cascadeRank = CASCADE.PLAY_MID;      // P1-P3: connecting/5s
			else                     cascadeRank = CASCADE.PLAY_LOW;      // P4-P5: any playable

			candidates.push({
				action: { type: ACTION.PLAY, target: order },
				value: 0,  // plays don't have clue-value; sorted within tier by priority
				cascadeRank,
				type: 'play',
				description: `Play ${logCard(card)} (slot ${slot}, priority ${priority})`
			});
		}
	}

	// Add discard candidates
	if (trash_orders.length > 0) {
		for (const order of trash_orders) {
			const card = state.deck[order];
			const slot = state.hands[state.ourPlayerIndex].indexOf(order) + 1;
			candidates.push({
				action: { type: ACTION.DISCARD, target: order },
				value: 0,
				cascadeRank: CASCADE.TRASH_EARLY,
				type: 'trash_discard',
				description: `Discard known trash ${logCard(card)} (slot ${slot})`
			});
		}
	}

	const chop = common.chop(state.hands[state.ourPlayerIndex]);
	if (chop !== undefined && !trash_orders.includes(chop)) {
		const card = state.deck[chop];
		const slot = state.hands[state.ourPlayerIndex].indexOf(chop) + 1;
		candidates.push({
			action: { type: ACTION.DISCARD, target: chop },
			value: 0,
			cascadeRank: CASCADE.DISCARD_CHOP,
			type: 'chop_discard',
			description: `Discard chop ${logCard(card)} (slot ${slot})`
		});
	}

	// Sort: cascade rank first (lower = higher priority), then value descending as tiebreaker
	candidates.sort((a, b) => a.cascadeRank !== b.cascadeRank
		? a.cascadeRank - b.cascadeRank
		: b.value - a.value
	);

	// Get bot's top recommendation
	let botAction;
	try {
		logger.off();
		botAction = await game.take_action();
		logger.on();
	}
	catch (e) {
		logger.on();
		botAction = candidates[0]?.action;
	}

	return { botAction, candidates };
}

/**
 * Classify how good the actual action was compared to the bot's recommendation.
 * @param {PerformAction} actual
 * @param {PerformAction} botAction
 * @param {object[]} candidates
 * @param {Action} resolvedAction - the resolved action after processing
 */
function classifyAction(actual, botAction, candidates, resolvedAction) {
	// Check for blunder conditions
	if (resolvedAction && resolvedAction.type === 'discard' && resolvedAction.failed) {
		return 'blunder';
	}

	if (!botAction) return 'unknown';

	// Check if action matches bot's top pick
	if (actionsMatch(actual, botAction)) {
		return 'correct';
	}

	// Find actual action in candidates
	const actualCandidate = candidates.find(c => actionsMatch(actual, c.action));
	const bestValue = candidates[0]?.value ?? 0;

	if (!actualCandidate) {
		// Action not even in candidate list
		return 'mistake';
	}

	const diff = bestValue - actualCandidate.value;

	if (diff <= 0.3) return 'good';
	if (diff <= 1.0) return 'inaccuracy';
	return 'mistake';
}

/**
 * Serialize game state snapshot for the viewer.
 * @param {State} state
 * @param {Identity[]} deck
 * @param {number} currentPlayerIndex
 */
function snapshotState(state, deck, currentPlayerIndex) {
	const hands = [];
	for (let p = 0; p < state.numPlayers; p++) {
		const hand = state.hands[p].map(order => {
			const card = deck[order];
			return {
				order,
				suitIndex: card?.suitIndex ?? -1,
				rank: card?.rank ?? -1,
				clued: state.deck[order]?.clued ?? false
			};
		});
		hands.push(hand);
	}

	return {
		score: state.score,
		maxScore: state.maxScore,
		clueTokens: state.clue_tokens,
		strikes: state.strikes,
		pace: state.pace,
		cardsLeft: state.cardsLeft,
		turnCount: state.turn_count,
		currentPlayerIndex,
		playStacks: [...state.play_stacks],
		hands,
		numSuits: state.variant.suits.length,
		suits: state.variant.suits,
		shortForms: state.variant.shortForms
	};
}

/**
 * Core analysis function — runs the bot on every turn and returns structured review data.
 * @param {object} game_data  Raw game JSON (players, deck, actions, options)
 * @param {number} level      H-Group convention level (1-MAX_H_LEVEL)
 * @param {string} [gameId]   Optional identifier for the game
 * @returns {Promise<object>}  The review data object
 */
export async function analyzeGame(game_data, level, gameId) {
	const { players, deck, actions, options = {} } = game_data;
	const variant = await getVariant(options?.variant ?? 'No Variant');
	Utils.globalModify({ variant, playerNames: players, cache: new Map() });

	// Suppress logging during analysis
	logger.setLevel(logger.LEVELS.ERROR);

	// Run analysis from each player's perspective
	const allTurnData = [];

	for (let ourPlayerIndex = 0; ourPlayerIndex < players.length; ourPlayerIndex++) {
		let order = 0;
		const state = new State(players, ourPlayerIndex, variant, options);
		const game = new HGroup(state, false, undefined, level);
		game.catchup = true;

		const bot = { game };

		// Draw starting hands
		for (let playerIndex = 0; playerIndex < state.numPlayers; playerIndex++) {
			for (let i = 0; i < state.handSize; i++) {
				const { suitIndex, rank } = playerIndex !== state.ourPlayerIndex ? deck[order] : { suitIndex: -1, rank: -1 };
				bot.game = bot.game.handle_action({ type: 'draw', playerIndex, order, suitIndex, rank });
				order++;
			}
		}

		let currentPlayerIndex = 0, turn = 0;

		for (let actionIndex = 0; actionIndex < actions.length; actionIndex++) {
			const action = actions[actionIndex];

			// Skip end game actions
			if (action.type === ACTION.END_GAME) break;

			if (turn !== 0)
				bot.game = bot.game.handle_action({ type: 'turn', num: turn, currentPlayerIndex });

			// If this is the current player's perspective, analyze before acting
			if (currentPlayerIndex === ourPlayerIndex) {
				let analysis = null;
				try {
					const result = await analyzePosition(bot.game);
					const performAction = { type: action.type, target: action.target, value: action.value ?? 0 };
					const resolvedAction = Utils.performToAction(bot.game.state, performAction, currentPlayerIndex, deck);

					const classification = classifyAction(performAction, result.botAction, result.candidates, resolvedAction);

					const botDesc = result.botAction ?
						describeAction(bot.game.state, result.botAction, deck, currentPlayerIndex) : 'Unknown';

					analysis = {
						turn,
						playerIndex: currentPlayerIndex,
						playerName: players[currentPlayerIndex],
						actualAction: describeAction(bot.game.state, performAction, deck, currentPlayerIndex),
						actualPerform: { type: action.type, target: action.target, value: action.value },
						botRecommendation: botDesc,
						botPerform: result.botAction,
						classification,
						candidates: result.candidates.slice(0, 10).map(c => ({
							description: c.description,
							value: Math.round(c.value * 100) / 100,
							cascadeRank: c.cascadeRank,
							type: c.type,
							action: { type: c.action.type, target: c.action.target, value: c.action.value },
							breakdown: c.breakdown || null,
							isActual: actionsMatch({ type: action.type, target: action.target, value: action.value ?? 0 }, c.action),
							isBot: result.botAction ? actionsMatch(result.botAction, c.action) : false
						})),
						state: snapshotState(bot.game.state, deck, currentPlayerIndex)
					};
				}
				catch (e) {
					analysis = {
						turn,
						playerIndex: currentPlayerIndex,
						playerName: players[currentPlayerIndex],
						actualAction: 'Error during analysis',
						classification: 'unknown',
						candidates: [],
						state: snapshotState(bot.game.state, deck, currentPlayerIndex)
					};
				}

				// Store by turn index
				if (!allTurnData[actionIndex]) allTurnData[actionIndex] = {};
				allTurnData[actionIndex] = analysis;
			}

			// Process the action (same as replay.js)
			const performAction = { type: action.type, target: action.target, value: action.value ?? 0 };
			bot.game = bot.game.handle_action(Utils.performToAction(bot.game.state, performAction, currentPlayerIndex, deck));

			if ((action.type === ACTION.PLAY || action.type === ACTION.DISCARD) && order < deck.length) {
				const { suitIndex, rank } = currentPlayerIndex !== ourPlayerIndex ? deck[order] : { suitIndex: -1, rank: -1 };
				bot.game = bot.game.handle_action({ type: 'draw', playerIndex: currentPlayerIndex, order, suitIndex, rank });
				order++;
			}

			if (action.type === ACTION.PLAY && bot.game.state.strikes === 3)
				break;

			currentPlayerIndex = (currentPlayerIndex + 1) % players.length;
			turn++;
		}
	}

	// Flatten turn data (filter out undefined)
	const turnData = allTurnData.filter(t => t !== undefined && t !== null);

	// Compute summary stats
	const classificationCounts = { correct: 0, good: 0, inaccuracy: 0, mistake: 0, blunder: 0, unknown: 0 };
	const perPlayer = {};
	for (const player of players) {
		perPlayer[player] = { correct: 0, good: 0, inaccuracy: 0, mistake: 0, blunder: 0, unknown: 0, total: 0 };
	}

	for (const t of turnData) {
		classificationCounts[t.classification] = (classificationCounts[t.classification] || 0) + 1;
		if (perPlayer[t.playerName]) {
			perPlayer[t.playerName][t.classification] = (perPlayer[t.playerName][t.classification] || 0) + 1;
			perPlayer[t.playerName].total++;
		}
	}

	const total = turnData.length;
	const accuracy = total > 0 ? Math.round(((classificationCounts.correct + classificationCounts.good) / total) * 100) : 0;

	return {
		gameInfo: {
			players,
			variant: variant.name,
			suits: variant.suits,
			shortForms: variant.shortForms,
			level,
			numSuits: variant.suits.length,
			id: gameId ?? null,
			deck: deck.map(c => ({ suitIndex: c.suitIndex, rank: c.rank }))
		},
		summary: {
			totalTurns: total,
			accuracy,
			classifications: classificationCounts,
			perPlayer
		},
		turns: turnData
	};
}

async function main() {
	if (Number(process.versions.node.split('.')[0]) < 22)
		throw new Error(`This program requires Node v22 or above! Currently using Node v${process.versions.node}.`);

	const { id, file, level: lStr = '1' } = Utils.parse_args();

	if (id === undefined && file === undefined)
		throw new Error('Provide either id=<number> or file=<path>');

	if (id !== undefined && file !== undefined)
		throw new Error('Provide either id or file, not both.');

	const level = Number(lStr);
	if (!Number.isInteger(level) || level < 1 || level > MAX_H_LEVEL)
		throw new Error(`Invalid level (${lStr}). Must be 1-${MAX_H_LEVEL}.`);

	let game_data;
	try {
		game_data = id !== undefined ? await fetchReplay(id) : JSON.parse(fs.readFileSync(file, 'utf8'));
	}
	catch (err) {
		throw new Error(`Failed to load game data: ${err}`);
	}

	console.log(`Analyzing game with ${game_data.players.length} players at level ${level}...`);
	console.log(`Players: ${game_data.players.join(', ')}`);
	console.log(`Actions: ${game_data.actions.length}`);

	const reviewData = await analyzeGame(game_data, level, id ?? file);

	console.log(`Variant: ${reviewData.gameInfo.variant}`);

	// Read the template and inject data
	const templatePath = path.resolve(import.meta.dirname, 'review-template.html');
	let template;
	try {
		template = fs.readFileSync(templatePath, 'utf8');
	}
	catch (e) {
		throw new Error(`Could not read template at ${templatePath}: ${e}`);
	}

	const outputHtml = template.replace(
		'/*__REVIEW_DATA__*/',
		`const REVIEW_DATA = ${JSON.stringify(reviewData)};`
	);

	const outputName = id ? `review-${id}.html` : `review-${path.basename(file, '.json')}.html`;
	fs.writeFileSync(outputName, outputHtml);
	console.log(`\nReview written to ${outputName}`);
	console.log(`Accuracy: ${reviewData.summary.accuracy}% (${reviewData.summary.classifications.correct + reviewData.summary.classifications.good} correct/good out of ${reviewData.summary.totalTurns} turns)`);
	console.log(`Classifications: ${JSON.stringify(reviewData.summary.classifications)}`);

	process.exit(0);
}

// Only run main() when executed directly (not imported)
import { fileURLToPath } from 'url';
const __filename = fileURLToPath(import.meta.url);
if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(__filename)) {
	main();
}
