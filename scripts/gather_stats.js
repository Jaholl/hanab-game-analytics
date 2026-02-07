// Gather playstyle percentile tables from 100+ players via the backend API
// Requires the backend to be running (each API call analyzes ~50 games, takes 30-60s)
//
// Usage: node scripts/gather_stats.js [--api-url URL] [--delay-ms MS] [--target N]

const args = process.argv.slice(2)
function getArg(name, defaultVal) {
  const idx = args.indexOf(name)
  return idx >= 0 && idx + 1 < args.length ? args[idx + 1] : defaultVal
}

const API_URL = getArg('--api-url', 'http://localhost:5191')
const DELAY_MS = parseInt(getArg('--delay-ms', '5000'))
const TARGET_PLAYERS = parseInt(getArg('--target', '100'))
const DISCOVERY_DELAY_MS = 150

async function fetchJSON(url) {
  for (let attempt = 0; attempt < 3; attempt++) {
    try {
      const res = await fetch(url)
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      return res.json()
    } catch (e) {
      if (attempt < 2) await sleep(1000 * (attempt + 1))
      else throw e
    }
  }
}

async function sleep(ms) { return new Promise(r => setTimeout(r, ms)) }

async function main() {
  console.log(`Backend: ${API_URL}`)
  console.log(`Delay between API calls: ${DELAY_MS}ms`)
  console.log(`Target players: ${TARGET_PLAYERS}`)

  // Phase 1: Discover players via global No Variant game history
  // Scan enough pages to find many unique players, then verify each via their history
  const playerNames = new Set()
  const PAGES_TO_FETCH = 50 // 50 pages × 100 games = 5000 games scanned

  console.log('\n=== Phase 1: Discovering player names from global No Variant games ===')
  for (let page = 0; page < PAGES_TO_FETCH; page++) {
    try {
      const data = await fetchJSON(`https://hanab.live/api/v1/variants/0?size=100&page=${page}`)
      const games = data.rows || []
      if (games.length === 0) {
        console.log(`  page ${page}: no more games, stopping`)
        break
      }
      for (const g of games) {
        g.users.split(', ').forEach(u => playerNames.add(u.trim()))
      }
      console.log(`  page ${page}: ${games.length} games, ${playerNames.size} unique players so far`)
      await sleep(DISCOVERY_DELAY_MS)
    } catch (e) {
      console.log(`  page ${page}: FAILED (${e.message})`)
    }
  }

  console.log(`\nFound ${playerNames.size} unique player names`)

  // Phase 1b: Verify each candidate has 50+ No Variant games via their history
  const qualifying = []
  console.log('\n=== Phase 1b: Verifying 50+ No Variant games ===')

  for (const name of playerNames) {
    if (qualifying.length >= TARGET_PLAYERS) break
    try {
      const data = await fetchJSON(`https://hanab.live/api/v1/history/${encodeURIComponent(name)}?size=100`)
      const v0Count = (data.rows || []).filter(r => r.variant === 0).length
      if (v0Count >= 50) {
        qualifying.push(name)
        console.log(`  ${name}: ${v0Count} v0 games - QUALIFIES (${qualifying.length}/${TARGET_PLAYERS})`)
      }
      await sleep(DISCOVERY_DELAY_MS)
    } catch (e) {
      console.log(`  ${name}: FAILED (${e.message})`)
    }
  }

  console.log(`\n${qualifying.length} qualifying players`)

  // Phase 2: Call backend playstyle API for each player
  console.log('\n=== Phase 2: Fetching playstyle from backend API ===')
  console.log(`  (each call analyzes ~50 games, expect 30-60s per player)\n`)

  const allRates = []

  for (let i = 0; i < qualifying.length; i++) {
    const player = qualifying[i]
    const url = `${API_URL}/hanabi/history/${encodeURIComponent(player)}/playstyle?size=50&level=2`
    try {
      const start = Date.now()
      const data = await fetchJSON(url)
      const elapsed = ((Date.now() - start) / 1000).toFixed(1)

      if (data.gamesAnalyzed < 10 || data.totalActions === 0) {
        console.log(`  [${i + 1}/${qualifying.length}] ${player}: only ${data.gamesAnalyzed} games, skipping (${elapsed}s)`)
        continue
      }

      allRates.push({
        name: player,
        games: data.gamesAnalyzed,
        playRate: data.rates.playRate,
        discardRate: data.rates.discardRate,
        clueRate: data.rates.clueRate,
        errorRate: data.rates.errorRate,
        missedSavesPerGame: data.rates.missedSavesPerGame,
        missedTechPerGame: data.rates.missedTechPerGame,
        misplayRate: data.rates.misplayRate,
      })

      console.log(`  [${i + 1}/${qualifying.length}] ${player}: ${data.gamesAnalyzed} games, ${elapsed}s`)
    } catch (e) {
      console.log(`  [${i + 1}/${qualifying.length}] ${player}: FAILED (${e.message})`)
    }

    if (i + 1 < qualifying.length) await sleep(DELAY_MS)
  }

  console.log(`\n${allRates.length} players with valid data`)

  // Phase 3: Compute percentile tables
  const rateKeys = [
    { key: 'playRate', field: 'playRate', csharpName: 'PlayRatePercentiles' },
    { key: 'discardRate', field: 'discardRate', csharpName: 'DiscardRatePercentiles' },
    { key: 'clueRate', field: 'clueRate', csharpName: 'ClueRatePercentiles' },
    { key: 'errorRate', field: 'errorRate', csharpName: 'ErrorRatePercentiles' },
    { key: 'missedSavesPerGame', field: 'missedSavesPerGame', csharpName: 'MissedSavesPerGamePercentiles' },
    { key: 'missedTechPerGame', field: 'missedTechPerGame', csharpName: 'MissedTechPerGamePercentiles' },
    { key: 'misplayRate', field: 'misplayRate', csharpName: 'MisplayRatePercentiles' },
  ]

  console.log('\n=== Per-Player Rates ===')
  console.log(
    'Player'.padEnd(22) +
    'Games'.padStart(6) +
    ' PlayR'.padStart(8) +
    ' DiscR'.padStart(8) +
    ' ClueR'.padStart(8) +
    ' ErrR'.padStart(8) +
    ' MSv/G'.padStart(8) +
    ' MTch/G'.padStart(8) +
    ' MispR'.padStart(8)
  )
  console.log('-'.repeat(86))
  for (const p of allRates.sort((a, b) => b.games - a.games)) {
    console.log(
      p.name.padEnd(22) +
      String(p.games).padStart(6) +
      p.playRate.toFixed(4).padStart(8) +
      p.discardRate.toFixed(4).padStart(8) +
      p.clueRate.toFixed(4).padStart(8) +
      p.errorRate.toFixed(4).padStart(8) +
      p.missedSavesPerGame.toFixed(2).padStart(8) +
      p.missedTechPerGame.toFixed(2).padStart(8) +
      p.misplayRate.toFixed(4).padStart(8)
    )
  }

  if (allRates.length === 0) {
    console.log('\nNo player data collected. Cannot compute percentiles.')
    return
  }

  console.log('\n=== Distribution Summary ===')
  for (const { key, field } of rateKeys) {
    const vals = allRates.map(p => p[field]).sort((a, b) => a - b)
    const n = vals.length
    const pct = (p) => vals[Math.min(Math.floor(n * p), n - 1)]
    console.log(`  ${key.padEnd(22)}: min=${vals[0].toFixed(4)} p25=${pct(0.25).toFixed(4)} p50=${pct(0.5).toFixed(4)} p75=${pct(0.75).toFixed(4)} max=${vals[n - 1].toFixed(4)}`)
  }

  console.log('\n=== Percentile Lookup Tables (paste into HanabiController.cs) ===')
  for (const { key, field, csharpName } of rateKeys) {
    const vals = allRates.map(p => p[field]).sort((a, b) => a - b)
    const n = vals.length
    const pcts = [0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0]
    const table = pcts.map(p => vals[Math.min(Math.floor(n * p), n - 1)].toFixed(4))
    console.log(`    private static readonly double[] ${csharpName} =`)
    console.log(`        { ${table.join(', ')} };`)
  }
}

main().catch(console.error)
