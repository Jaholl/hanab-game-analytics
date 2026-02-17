import express from 'express';
import { analyzeGame, fetchReplay } from './review.js';
import { MAX_H_LEVEL } from './constants.js';

const app = express();
const PORT = process.env.PORT || 3001;

// CORS
app.use((req, res, next) => {
	const allowedOrigins = [
		'http://localhost:5173',
		'http://localhost:5174',
		'http://localhost:5175',
		'http://localhost:5176',
		'https://hanab-frontend.vercel.app',
	];
	const origin = req.headers.origin;
	if (allowedOrigins.includes(origin) || (origin && origin.startsWith('https://hanab-frontend-') && origin.endsWith('.vercel.app'))) {
		res.setHeader('Access-Control-Allow-Origin', origin);
	}
	res.setHeader('Access-Control-Allow-Methods', 'GET, OPTIONS');
	res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
	if (req.method === 'OPTIONS') return res.sendStatus(204);
	next();
});

// In-flight request tracking to avoid duplicate work
const inFlight = new Map();

app.get('/api/review/:gameId', async (req, res) => {
	const { gameId } = req.params;
	const level = Number(req.query.level ?? 5);

	if (!gameId || isNaN(Number(gameId))) {
		return res.status(400).json({ error: 'gameId must be a number' });
	}

	if (!Number.isInteger(level) || level < 1 || level > MAX_H_LEVEL) {
		return res.status(400).json({ error: `level must be 1-${MAX_H_LEVEL}` });
	}

	const cacheKey = `${gameId}:${level}`;

	// Deduplicate in-flight requests
	if (inFlight.has(cacheKey)) {
		try {
			const result = await inFlight.get(cacheKey);
			return res.json(result);
		} catch (err) {
			return res.status(500).json({ error: err.message });
		}
	}

	const promise = (async () => {
		const gameData = await fetchReplay(gameId);
		return analyzeGame(gameData, level, gameId);
	})();

	inFlight.set(cacheKey, promise);

	try {
		const result = await promise;
		res.json(result);
	} catch (err) {
		console.error(`Error analyzing game ${gameId}:`, err);
		res.status(500).json({ error: err.message || 'Analysis failed' });
	} finally {
		inFlight.delete(cacheKey);
	}
});

app.get('/api/health', (_req, res) => {
	res.json({ status: 'ok' });
});

app.listen(PORT, () => {
	console.log(`Hanabi bot API running on http://localhost:${PORT}`);
	console.log(`Try: http://localhost:${PORT}/api/review/1752490?level=5`);
});
