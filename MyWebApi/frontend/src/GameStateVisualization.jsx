import { motion } from 'motion/react'

const SUIT_NAMES = ['Red', 'Yellow', 'Green', 'Blue', 'Purple']
const SUIT_COLORS = ['#ff4444', '#ffdd44', '#44dd44', '#4488ff', '#aa44ff']
const SUIT_ABBREVIATIONS = ['R', 'Y', 'G', 'B', 'P']

function GameStateVisualization({ state, highlightedDeckIndex, players, currentPlayerOverride, previousAction, currentTurn, violationTurn, minTurn, onPrevTurn, onNextTurn }) {
  if (!state) {
    return (
      <div className="game-state-unavailable">
        State unavailable
      </div>
    )
  }

  // Use override if provided (for showing who is ABOUT to act, not who just acted)
  const currentPlayer = currentPlayerOverride !== undefined ? currentPlayerOverride : state.currentPlayer

  // Determine if previous action was a clue and what it targeted
  const prevClue = previousAction && (previousAction.type === 2 || previousAction.type === 3)
    ? {
        targetPlayer: previousAction.target,
        isColor: previousAction.type === 2,
        value: previousAction.value
      }
    : null

  // Check if a card was touched by the previous clue
  const wasCardClued = (card, playerIndex) => {
    if (!prevClue || playerIndex !== prevClue.targetPlayer) return false
    if (prevClue.isColor) {
      return card.suitIndex === prevClue.value
    } else {
      return card.rank === prevClue.value
    }
  }

  // Format previous action description
  const getPrevActionDescription = () => {
    if (!previousAction) return null
    const actorIndex = (currentPlayer - 1 + players.length) % players.length
    const actor = players?.[actorIndex] || `Player ${actorIndex + 1}`

    switch (previousAction.type) {
      case 0: return `${actor} played a card`
      case 1: return `${actor} discarded`
      case 2: return `${actor} clued ${SUIT_NAMES[previousAction.value]} to ${players?.[previousAction.target] || `Player ${previousAction.target + 1}`}`
      case 3: return `${actor} clued ${previousAction.value} to ${players?.[previousAction.target] || `Player ${previousAction.target + 1}`}`
      default: return null
    }
  }

  const deckRemaining = state.deckIndex !== undefined
    ? 50 - state.deckIndex - state.hands.reduce((acc, h) => acc + h.length, 0)
    : 0

  return (
    <motion.div
      className="game-state-visualization"
      initial={{ opacity: 0, height: 0 }}
      animate={{ opacity: 1, height: 'auto' }}
      exit={{ opacity: 0, height: 0 }}
      transition={{ duration: 0.3 }}
    >
      {/* Turn Navigation */}
      {violationTurn && minTurn < violationTurn && (
        <div className="turn-nav-bar" onClick={e => e.stopPropagation()}>
          <button
            className="turn-nav-btn"
            disabled={currentTurn <= minTurn}
            onClick={onPrevTurn}
          >
            &lt; Prev
          </button>
          <span className={`turn-nav-indicator ${currentTurn < violationTurn ? 'viewing-history' : ''}`}>
            Turn {currentTurn} of {violationTurn}
          </span>
          <button
            className="turn-nav-btn"
            disabled={currentTurn >= violationTurn}
            onClick={onNextTurn}
          >
            Next &gt;
          </button>
        </div>
      )}

      {/* Previous Action */}
      {previousAction && (
        <div className="previous-action-bar">
          <span className="prev-action-label">Previous action:</span>
          <span className="prev-action-description">{getPrevActionDescription()}</span>
        </div>
      )}

      {/* Status Bar */}
      <div className="state-status-bar">
        <div className="status-item">
          <span className="status-label">Turn</span>
          <span className="status-value">{state.turn}</span>
        </div>
        <div className="status-item">
          <span className="status-label">Clues</span>
          <span className="status-value clues">{state.clueTokens}</span>
        </div>
        <div className="status-item">
          <span className="status-label">Strikes</span>
          <span className="status-value strikes">{state.strikes}/3</span>
        </div>
        <div className="status-item">
          <span className="status-label">Deck</span>
          <span className="status-value">{deckRemaining}</span>
        </div>
        <div className="status-item">
          <span className="status-label">Score</span>
          <span className="status-value score">{state.score}</span>
        </div>
      </div>

      {/* Play Stacks */}
      <div className="state-section">
        <div className="state-section-title">Play Stacks</div>
        <div className="play-stacks">
          {state.playStacks.map((value, suitIndex) => (
            <div
              key={suitIndex}
              className="play-stack"
              style={{ '--suit-color': SUIT_COLORS[suitIndex] }}
            >
              <div className="stack-suit">{SUIT_ABBREVIATIONS[suitIndex]}</div>
              <div className="stack-value">{value}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Player Hands */}
      <div className="state-section">
        <div className="state-section-title">Player Hands (Omniscient View)</div>
        <div className="player-hands">
          {state.hands.map((hand, playerIndex) => (
            <div key={playerIndex} className="player-hand">
              <div className="hand-player-name">
                {players?.[playerIndex] || `Player ${playerIndex + 1}`}
                {playerIndex === currentPlayer && (
                  <span className="current-player-indicator"> (current)</span>
                )}
              </div>
              <div className="hand-cards">
                {hand.map((card) => {
                  const isHighlighted = card.deckIndex === highlightedDeckIndex
                  const wasClued = wasCardClued(card, playerIndex)
                  return (
                    <div
                      key={card.deckIndex}
                      className={`hand-card ${isHighlighted ? 'highlighted-card' : ''} ${wasClued ? 'just-clued' : ''} ${card.hasAnyClue ? 'has-clues' : ''}`}
                      style={{ '--suit-color': SUIT_COLORS[card.suitIndex] }}
                    >
                      <div className="card-face">
                        <span className="card-suit">{SUIT_ABBREVIATIONS[card.suitIndex]}</span>
                        <span className="card-rank">{card.rank}</span>
                      </div>
                      {card.hasAnyClue && (
                        <div className="card-clue-indicators">
                          {card.clueColors.some(c => c) && (
                            <span className="clue-indicator color-clued">C</span>
                          )}
                          {card.clueRanks.some(r => r) && (
                            <span className="clue-indicator rank-clued">R</span>
                          )}
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
            </div>
          ))}
        </div>
      </div>
    </motion.div>
  )
}

export default GameStateVisualization
