import { useState, useEffect, useMemo } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import {
  AreaChart, Area, BarChart, Bar, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend
} from 'recharts'
import GameDetail from './GameDetail'
import './App.css'

// Custom tooltip component for charts
const CustomTooltip = ({ active, payload, label }) => {
  if (active && payload && payload.length) {
    return (
      <div className="custom-tooltip">
        <p className="tooltip-label">{label}</p>
        {payload.map((entry, index) => (
          <p key={index} className="tooltip-value" style={{ color: entry.color }}>
            {entry.name}: {entry.value}
          </p>
        ))}
      </div>
    )
  }
  return null
}

// Score tier classification
const getScoreTier = (score) => {
  if (score === 25) return 'perfect'
  if (score >= 22) return 'excellent'
  if (score >= 18) return 'good'
  if (score >= 12) return 'average'
  return 'low'
}

// Format date for display
const formatDate = (dateString) => {
  if (!dateString) return ''
  const date = new Date(dateString)
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

// Chart colors matching theme
const CHART_COLORS = {
  ember: '#ff6b35',
  flame: '#ff9f1c',
  gold: '#ffbe0b',
  rose: '#ff006e',
  violet: '#8338ec',
  cyan: '#3a86ff',
  mint: '#06ffa5'
}

const PIE_COLORS = ['#ffbe0b', '#06ffa5', '#3a86ff', '#8338ec', '#ff006e']

function App() {
  const [games, setGames] = useState([])
  const [totalRows, setTotalRows] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [username, setUsername] = useState('jaholl')
  const [activeView, setActiveView] = useState('all')

  // Game detail state
  const [selectedGameId, setSelectedGameId] = useState(null)
  const [gameAnalysis, setGameAnalysis] = useState(null)
  const [analysisLoading, setAnalysisLoading] = useState(false)
  const [analysisError, setAnalysisError] = useState(null)

  const fetchHistory = async () => {
    setLoading(true)
    setError(null)
    try {
      const response = await fetch(`http://localhost:5191/hanabi/history/${username}?size=100`)
      if (!response.ok) throw new Error('Failed to fetch game history')
      const data = await response.json()
      setGames(data.rows || [])
      setTotalRows(data.total_rows || 0)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchHistory()
  }, [])

  const handleSubmit = (e) => {
    e.preventDefault()
    fetchHistory()
  }

  const fetchGameAnalysis = async (gameId) => {
    setSelectedGameId(gameId)
    setAnalysisLoading(true)
    setAnalysisError(null)
    setGameAnalysis(null)

    try {
      const response = await fetch(`http://localhost:5191/hanabi/game/${gameId}/analysis`)
      if (!response.ok) {
        const text = await response.text()
        throw new Error(text || 'Failed to load game analysis')
      }
      const data = await response.json()
      setGameAnalysis(data)
    } catch (err) {
      setAnalysisError(err.message)
    } finally {
      setAnalysisLoading(false)
    }
  }

  const handleBackToList = () => {
    setSelectedGameId(null)
    setGameAnalysis(null)
    setAnalysisError(null)
  }

  // Compute statistics
  const stats = useMemo(() => {
    if (games.length === 0) return null

    const scores = games.map(g => g.score)
    const avgScore = (scores.reduce((a, b) => a + b, 0) / scores.length).toFixed(1)
    const maxScore = Math.max(...scores)
    const perfectGames = scores.filter(s => s === 25).length
    const winRate = ((perfectGames / scores.length) * 100).toFixed(1)

    return { avgScore, maxScore, perfectGames, winRate, totalGames: games.length }
  }, [games])

  // Score distribution data for bar chart
  const scoreDistribution = useMemo(() => {
    if (games.length === 0) return []

    const distribution = {}
    games.forEach(g => {
      const score = g.score
      distribution[score] = (distribution[score] || 0) + 1
    })

    return Object.entries(distribution)
      .map(([score, count]) => ({ score: parseInt(score), count }))
      .sort((a, b) => a.score - b.score)
  }, [games])

  // Score trend over time (recent games)
  const scoreTrend = useMemo(() => {
    if (games.length === 0) return []

    return [...games]
      .slice(0, 30)
      .reverse()
      .map((g, i) => ({
        game: i + 1,
        score: g.score,
        date: formatDate(g.dateTime)
      }))
  }, [games])

  // Player count distribution for pie chart
  const playerDistribution = useMemo(() => {
    if (games.length === 0) return []

    const distribution = {}
    games.forEach(g => {
      const count = g.numPlayers
      distribution[count] = (distribution[count] || 0) + 1
    })

    return Object.entries(distribution)
      .map(([players, count]) => ({
        name: `${players} Players`,
        value: count
      }))
      .sort((a, b) => parseInt(a.name) - parseInt(b.name))
  }, [games])

  // Score tier breakdown
  const tierBreakdown = useMemo(() => {
    if (games.length === 0) return []

    const tiers = { perfect: 0, excellent: 0, good: 0, average: 0, low: 0 }
    games.forEach(g => {
      tiers[getScoreTier(g.score)]++
    })

    return [
      { name: 'Perfect (25)', value: tiers.perfect, color: CHART_COLORS.gold },
      { name: 'Excellent (22-24)', value: tiers.excellent, color: CHART_COLORS.mint },
      { name: 'Good (18-21)', value: tiers.good, color: CHART_COLORS.cyan },
      { name: 'Average (12-17)', value: tiers.average, color: CHART_COLORS.violet },
      { name: 'Low (0-11)', value: tiers.low, color: CHART_COLORS.rose }
    ].filter(t => t.value > 0)
  }, [games])

  // Show game detail view if a game is selected
  if (selectedGameId !== null) {
    return (
      <div className="app">
        <GameDetail
          gameId={selectedGameId}
          analysis={gameAnalysis}
          loading={analysisLoading}
          error={analysisError}
          onBack={handleBackToList}
        />
      </div>
    )
  }

  return (
    <div className="app">
      {/* Header */}
      <motion.header
        className="header"
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.6 }}
      >
        <div className="logo-container">
          <span className="logo-icon">🎆</span>
          <h1 className="title">Hanabi</h1>
        </div>
        <p className="subtitle">Game Analytics Dashboard</p>
      </motion.header>

      {/* Search */}
      <motion.div
        className="search-section"
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.6, delay: 0.1 }}
      >
        <form onSubmit={handleSubmit} className="search-form">
          <span className="search-icon">🔍</span>
          <input
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            placeholder="Enter username..."
            className="username-input"
          />
          <button type="submit" disabled={loading} className="search-btn">
            {loading ? 'Loading...' : 'Analyze'}
          </button>
        </form>
      </motion.div>

      {/* Error State */}
      <AnimatePresence>
        {error && (
          <motion.div
            className="error-message"
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.95 }}
          >
            <div className="error-icon">⚠️</div>
            <p>{error}</p>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Loading State */}
      {loading && (
        <div className="loading-container">
          <div className="loading-spinner"></div>
          <p className="loading-text">Fetching game history...</p>
        </div>
      )}

      {/* Main Content */}
      {!loading && !error && games.length > 0 && (
        <>
          {/* Stats Overview */}
          <motion.div
            className="stats-overview"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.2 }}
          >
            <div className="stat-card ember">
              <div className="stat-value">{totalRows}</div>
              <div className="stat-label">Total Games</div>
            </div>
            <div className="stat-card rose">
              <div className="stat-value">{stats?.avgScore}</div>
              <div className="stat-label">Avg Score</div>
            </div>
            <div className="stat-card ocean">
              <div className="stat-value">{stats?.maxScore}</div>
              <div className="stat-label">Best Score</div>
            </div>
            <div className="stat-card sunset">
              <div className="stat-value">{stats?.perfectGames}</div>
              <div className="stat-label">Perfect Games</div>
            </div>
          </motion.div>

          {/* View Toggle */}
          <motion.div
            className="view-controls"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.3 }}
          >
            <button
              className={`view-btn ${activeView === 'all' ? 'active' : ''}`}
              onClick={() => setActiveView('all')}
            >
              📊 All Views
            </button>
            <button
              className={`view-btn ${activeView === 'charts' ? 'active' : ''}`}
              onClick={() => setActiveView('charts')}
            >
              📈 Charts Only
            </button>
            <button
              className={`view-btn ${activeView === 'games' ? 'active' : ''}`}
              onClick={() => setActiveView('games')}
            >
              🎴 Games Only
            </button>
          </motion.div>

          {/* Charts Section */}
          {(activeView === 'all' || activeView === 'charts') && (
            <motion.section
              className="charts-section"
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.6, delay: 0.3 }}
            >
              <div className="charts-grid">
                {/* Score Trend */}
                <div className="chart-container">
                  <div className="chart-header">
                    <span className="chart-icon">📈</span>
                    <h3 className="chart-title">Score Trend (Recent Games)</h3>
                  </div>
                  <div className="chart-wrapper">
                    <ResponsiveContainer width="100%" height="100%">
                      <AreaChart data={scoreTrend}>
                        <defs>
                          <linearGradient id="scoreGradient" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor={CHART_COLORS.ember} stopOpacity={0.8}/>
                            <stop offset="95%" stopColor={CHART_COLORS.ember} stopOpacity={0.1}/>
                          </linearGradient>
                        </defs>
                        <CartesianGrid strokeDasharray="3 3" stroke="#30363d" />
                        <XAxis
                          dataKey="game"
                          stroke="#8b949e"
                          tick={{ fill: '#8b949e', fontSize: 12 }}
                        />
                        <YAxis
                          domain={[0, 25]}
                          stroke="#8b949e"
                          tick={{ fill: '#8b949e', fontSize: 12 }}
                        />
                        <Tooltip content={<CustomTooltip />} />
                        <Area
                          type="monotone"
                          dataKey="score"
                          stroke={CHART_COLORS.ember}
                          strokeWidth={2}
                          fill="url(#scoreGradient)"
                          name="Score"
                        />
                      </AreaChart>
                    </ResponsiveContainer>
                  </div>
                </div>

                {/* Score Distribution */}
                <div className="chart-container">
                  <div className="chart-header">
                    <span className="chart-icon">📊</span>
                    <h3 className="chart-title">Score Distribution</h3>
                  </div>
                  <div className="chart-wrapper">
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={scoreDistribution}>
                        <CartesianGrid strokeDasharray="3 3" stroke="#30363d" />
                        <XAxis
                          dataKey="score"
                          stroke="#8b949e"
                          tick={{ fill: '#8b949e', fontSize: 12 }}
                        />
                        <YAxis
                          stroke="#8b949e"
                          tick={{ fill: '#8b949e', fontSize: 12 }}
                        />
                        <Tooltip content={<CustomTooltip />} />
                        <Bar dataKey="count" name="Games" radius={[4, 4, 0, 0]}>
                          {scoreDistribution.map((entry, index) => (
                            <Cell
                              key={`cell-${index}`}
                              fill={entry.score === 25 ? CHART_COLORS.gold :
                                    entry.score >= 22 ? CHART_COLORS.mint :
                                    entry.score >= 18 ? CHART_COLORS.cyan :
                                    entry.score >= 12 ? CHART_COLORS.violet : CHART_COLORS.rose}
                            />
                          ))}
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>

                {/* Performance Tiers */}
                <div className="chart-container">
                  <div className="chart-header">
                    <span className="chart-icon">🎯</span>
                    <h3 className="chart-title">Performance Tiers</h3>
                  </div>
                  <div className="chart-wrapper">
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie
                          data={tierBreakdown}
                          cx="50%"
                          cy="50%"
                          innerRadius={60}
                          outerRadius={100}
                          paddingAngle={2}
                          dataKey="value"
                          label={({ name, percent }) => `${(percent * 100).toFixed(0)}%`}
                          labelLine={false}
                        >
                          {tierBreakdown.map((entry, index) => (
                            <Cell key={`cell-${index}`} fill={entry.color} />
                          ))}
                        </Pie>
                        <Tooltip content={<CustomTooltip />} />
                        <Legend
                          verticalAlign="bottom"
                          height={36}
                          formatter={(value) => <span style={{ color: '#c9d1d9' }}>{value}</span>}
                        />
                      </PieChart>
                    </ResponsiveContainer>
                  </div>
                </div>

                {/* Player Count Distribution */}
                <div className="chart-container">
                  <div className="chart-header">
                    <span className="chart-icon">👥</span>
                    <h3 className="chart-title">Games by Player Count</h3>
                  </div>
                  <div className="chart-wrapper">
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={playerDistribution} layout="vertical">
                        <CartesianGrid strokeDasharray="3 3" stroke="#30363d" />
                        <XAxis
                          type="number"
                          stroke="#8b949e"
                          tick={{ fill: '#8b949e', fontSize: 12 }}
                        />
                        <YAxis
                          type="category"
                          dataKey="name"
                          stroke="#8b949e"
                          tick={{ fill: '#8b949e', fontSize: 12 }}
                          width={80}
                        />
                        <Tooltip content={<CustomTooltip />} />
                        <Bar dataKey="value" name="Games" radius={[0, 4, 4, 0]}>
                          {playerDistribution.map((entry, index) => (
                            <Cell key={`cell-${index}`} fill={PIE_COLORS[index % PIE_COLORS.length]} />
                          ))}
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>
              </div>
            </motion.section>
          )}

          {/* Games List */}
          {(activeView === 'all' || activeView === 'games') && (
            <motion.section
              className="games-section"
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.6, delay: 0.4 }}
            >
              <div className="section-header">
                <h2 className="section-title">
                  <span>🎴</span> Recent Games
                </h2>
              </div>

              <div className="games-grid">
                {games.map((game, index) => {
                  const tier = getScoreTier(game.score)
                  return (
                    <motion.div
                      key={game.id}
                      className={`game-card ${tier}`}
                      initial={{ opacity: 0, y: 20 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ duration: 0.4, delay: index * 0.03 }}
                      whileHover={{ scale: 1.02 }}
                      onClick={() => fetchGameAnalysis(game.id)}
                    >
                      <div className="card-top">
                        <span className="game-id">#{game.id}</span>
                        <span className={`score-badge ${tier}`}>{game.score}</span>
                      </div>

                      <div className="player-info">
                        <span className="player-count">
                          👤 {game.numPlayers}
                        </span>
                        <span className="player-names">{game.users}</span>
                      </div>

                      <div className="card-footer">
                        <span className="datetime">{formatDate(game.dateTime)}</span>
                        <span className="seed">{game.seed}</span>
                      </div>
                    </motion.div>
                  )
                })}
              </div>
            </motion.section>
          )}
        </>
      )}

      {/* Empty State */}
      {!loading && !error && games.length === 0 && (
        <div className="empty-state">
          <div className="empty-icon">🎆</div>
          <p className="empty-text">No games found for this user. Try a different username!</p>
        </div>
      )}
    </div>
  )
}

export default App
