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

  // Phase 1: Discover players via prolific seed players on hanab.live
  const seedQueries = [
    { name: 'timotree', sizes: [100, 100, 100] },
    { name: 'sjdrodge', sizes: [100, 100] },
    { name: 'Gestalt', sizes: [100, 100] },
    { name: 'Jake_Stiles', sizes: [100] },
    { name: 'florrat2', sizes: [100] },
    { name: 'sodiumdebt', sizes: [100] },
    { name: 'jaholl', sizes: [100] },
    { name: 'FairyBoots', sizes: [100] },
    { name: 'Estabir', sizes: [100] },
    { name: 'robense', sizes: [100] },
    { name: 'Fireheart', sizes: [100] },
    { name: 'kimbifille', sizes: [100] },
    { name: 'pianoblook', sizes: [100] },
    { name: 'MarkusKaworworking', sizes: [100] },
    { name: 'Razvopp', sizes: [100] },
  ]

  const playerGameCounts = {} // player -> Set of game IDs

  console.log('\n=== Phase 1: Discovering players ===')
  for (const { name, sizes } of seedQueries) {
    for (let page = 0; page < sizes.length; page++) {
      try {
        const data = await fetchJSON(`https://hanab.live/api/v1/history/${name}?size=${sizes[page]}`)
        const v0Games = (data.rows || []).filter(r => r.variant === 0)
        for (const g of v0Games) {
          g.users.split(', ').forEach(u => {
            const p = u.trim()
            if (!playerGameCounts[p]) playerGameCounts[p] = new Set()
            playerGameCounts[p].add(g.id)
          })
        }
        console.log(`  ${name} page ${page}: ${v0Games.length} v0 games`)
        await sleep(DISCOVERY_DELAY_MS)
      } catch (e) {
        console.log(`  ${name} page ${page}: FAILED (${e.message})`)
      }
    }
  }

  // Sort candidates by number of observed games
  const candidates = Object.entries(playerGameCounts)
    .map(([name, ids]) => ({ name, observed: ids.size }))
    .sort((a, b) => b.observed - a.observed)

  console.log(`\nFound ${candidates.length} unique players`)

  // Phase 1b: Verify candidates have 50+ v0 games
  const qualifying = []
  console.log('\n=== Phase 1b: Verifying 50+ v0 games ===')

  for (const c of candidates) {
    if (qualifying.length >= TARGET_PLAYERS) break

    if (c.observed >= 50) {
      qualifying.push(c.name)
      console.log(`  ${c.name}: ${c.observed} observed (auto-qualify)`)
      continue
    }

    try {
      const data = await fetchJSON(`https://hanab.live/api/v1/history/${c.name}?size=100`)
      const v0Count = (data.rows || []).filter(r => r.variant === 0).length
      if (v0Count >= 50) {
        qualifying.push(c.name)
        console.log(`  ${c.name}: ${v0Count} v0 games - QUALIFIES`)
      }
      await sleep(DISCOVERY_DELAY_MS)
    } catch (e) {
      console.log(`  ${c.name}: FAILED (${e.message})`)
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
        badClueRate: data.rates.badClueRate,
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
    { key: 'badClueRate', field: 'badClueRate', csharpName: 'BadClueRatePercentiles' },
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
    ' BadClR'.padStart(8) +
    ' MSv/G'.padStart(8) +
    ' MTch/G'.padStart(8) +
    ' MispR'.padStart(8)
  )
  console.log('-'.repeat(94))
  for (const p of allRates.sort((a, b) => b.games - a.games)) {
    console.log(
      p.name.padEnd(22) +
      String(p.games).padStart(6) +
      p.playRate.toFixed(4).padStart(8) +
      p.discardRate.toFixed(4).padStart(8) +
      p.clueRate.toFixed(4).padStart(8) +
      p.errorRate.toFixed(4).padStart(8) +
      p.badClueRate.toFixed(4).padStart(8) +
      p.missedSavesPerGame.toFixed(2).padStart(8) +
      p.missedTechPerGame.toFixed(2).padStart(8) +
      p.misplayRate.toFixed(4).padStart(8)
    )
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
