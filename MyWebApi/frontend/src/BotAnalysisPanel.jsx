import { useState, useEffect } from 'react'
import { motion, AnimatePresence } from 'motion/react'

const BOT_API_URL = import.meta.env.VITE_BOT_API_URL || 'http://localhost:3001'

const SUIT_COLORS = ['#ff4444', '#ffdd44', '#44dd44', '#4488ff', '#aa44ff', '#ff88cc']

const CLASSIFICATION_CONFIG = {
  correct:    { label: 'Correct',    color: 'var(--mint)',  bg: 'rgba(6, 255, 165, 0.15)' },
  good:       { label: 'Good',       color: 'var(--cyan)',  bg: 'rgba(58, 134, 255, 0.15)' },
  inaccuracy: { label: 'Inaccuracy', color: 'var(--gold)',  bg: 'rgba(255, 190, 11, 0.15)' },
  mistake:    { label: 'Mistake',    color: 'var(--flame)', bg: 'rgba(255, 159, 28, 0.15)' },
  blunder:    { label: 'Blunder',    color: 'var(--rose)',  bg: 'rgba(255, 0, 110, 0.15)' },
  unknown:    { label: 'Unknown',    color: 'var(--text-muted)', bg: 'rgba(72, 79, 88, 0.15)' },
}

const CASCADE_LABELS = {
  1: 'Play into bluff',
  2: 'Play into hidden finesse',
  3: 'Urgent action',
  4: 'Generation discard',
  5: 'Finesse clue (next player)',
  6: 'Play finesse (P0)',
  7: 'Sarcastic discard',
  8: 'Play connecting/5s (P1-P3)',
  9: 'Discard known trash (early)',
  10: 'Play clue (MCVP)',
  11: 'Play any (P4-P5)',
  12: 'Stall clue',
  13: 'Discard known trash',
  14: 'Discard chop',
}

const TYPE_BADGES = {
  play_clue:     { label: 'Play Clue',  color: 'var(--mint)' },
  save_clue:     { label: 'Save Clue',  color: 'var(--cyan)' },
  stall_clue:    { label: 'Stall Clue', color: 'var(--violet)' },
  play:          { label: 'Play',        color: 'var(--gold)' },
  trash_discard: { label: 'Trash',       color: 'var(--text-secondary)' },
  chop_discard:  { label: 'Chop',        color: 'var(--flame)' },
}

function ClassificationBadge({ classification }) {
  const config = CLASSIFICATION_CONFIG[classification] || CLASSIFICATION_CONFIG.unknown
  return (
    <span
      className="bot-classification-badge"
      style={{ color: config.color, background: config.bg, borderColor: config.color }}
    >
      {config.label}
    </span>
  )
}

function AccuracyBar({ classifications, total }) {
  if (total === 0) return null
  const order = ['correct', 'good', 'inaccuracy', 'mistake', 'blunder', 'unknown']
  return (
    <div className="bot-accuracy-bar">
      {order.map(key => {
        const count = classifications[key] || 0
        if (count === 0) return null
        const pct = (count / total) * 100
        const config = CLASSIFICATION_CONFIG[key]
        return (
          <div
            key={key}
            className="bot-accuracy-segment"
            style={{ width: `${pct}%`, background: config.color }}
            title={`${config.label}: ${count} (${Math.round(pct)}%)`}
          />
        )
      })}
    </div>
  )
}

function MiniHandCard({ card, highlighted, suitNames }) {
  const suitColor = card.suitIndex >= 0 ? SUIT_COLORS[card.suitIndex] || '#888' : '#444'
  const isKnown = card.suitIndex >= 0 && card.rank >= 0
  return (
    <div
      className={`bot-mini-card ${highlighted ? 'bot-card-highlighted' : ''}`}
      style={{ '--suit-color': suitColor }}
      title={isKnown ? `${suitNames?.[card.suitIndex] || 'Suit ' + card.suitIndex} ${card.rank}` : 'Unknown'}
    >
      {isKnown && <span className="bot-mini-rank">{card.rank}</span>}
    </div>
  )
}

function CandidateRow({ candidate, isExpanded, onToggle, highlightedCards, onHover }) {
  const typeBadge = TYPE_BADGES[candidate.type] || { label: candidate.type, color: 'var(--text-muted)' }
  const cascadeLabel = CASCADE_LABELS[candidate.cascadeRank] || `Priority ${candidate.cascadeRank}`

  // Determine which cards this candidate targets (for hover highlighting)
  const getTargetOrders = () => {
    const action = candidate.action
    if (action.type === 0 || action.type === 1) return [action.target] // play/discard
    return [] // clues highlight via clue target — not card orders
  }

  return (
    <div
      className={`bot-candidate-row ${candidate.isActual ? 'is-actual' : ''} ${candidate.isBot ? 'is-bot' : ''}`}
      onMouseEnter={() => onHover(getTargetOrders())}
      onMouseLeave={() => onHover([])}
    >
      <div className="bot-candidate-main" onClick={candidate.breakdown ? onToggle : undefined}>
        <span className="bot-candidate-rank">#{candidate.cascadeRank}</span>
        <span className="bot-candidate-desc">{candidate.description}</span>
        <span className="bot-candidate-type" style={{ color: typeBadge.color, borderColor: typeBadge.color }}>
          {typeBadge.label}
        </span>
        {candidate.value > 0 && (
          <span className="bot-candidate-value">{candidate.value.toFixed(2)}</span>
        )}
        <span className="bot-candidate-markers">
          {candidate.isBot && <span className="bot-marker" title="Bot recommendation">BOT</span>}
          {candidate.isActual && <span className="actual-marker" title="Actual action played">PLAYED</span>}
        </span>
        {candidate.breakdown && (
          <span className="bot-candidate-expand">{isExpanded ? '−' : '+'}</span>
        )}
      </div>
      <AnimatePresence>
        {isExpanded && candidate.breakdown && (
          <motion.div
            className="bot-candidate-breakdown"
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2 }}
          >
            <BreakdownTable breakdown={candidate.breakdown} />
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}

function BreakdownTable({ breakdown }) {
  const b = breakdown
  const rows = [
    { label: 'Finesses', value: b.finesses, detail: b.finesseDetails?.join(', ') },
    { label: 'Playables', value: b.playables, detail: b.playableDetails?.join(', ') },
    { label: 'New touched', value: `${b.newTouched} (${b.newTouchedValue})` },
    { label: 'Bad touch', value: b.badTouch, detail: b.badTouchDetails?.join(', '), negative: true },
    { label: 'CM dupe', value: b.cmDupe, negative: true },
    { label: 'Avoidable dupe', value: b.avoidableDupe, negative: true },
    { label: 'Elim', value: b.elim },
    { label: 'Remainder', value: b.remainder, negative: true },
    { label: 'Precision', value: b.precision },
  ]
  return (
    <div className="bot-breakdown-grid">
      {rows.map((row, i) => (
        <div key={i} className="bot-breakdown-row">
          <span className="bot-breakdown-label">{row.label}</span>
          <span className={`bot-breakdown-value ${row.negative && row.value > 0 ? 'negative' : ''}`}>
            {typeof row.value === 'number' ? (row.negative && row.value > 0 ? `−${row.value}` : row.value) : row.value}
          </span>
          {row.detail && <span className="bot-breakdown-detail">{row.detail}</span>}
        </div>
      ))}
    </div>
  )
}

function TurnCard({ turn, suitNames, onSelectTurn, isSelected }) {
  const config = CLASSIFICATION_CONFIG[turn.classification] || CLASSIFICATION_CONFIG.unknown
  return (
    <motion.div
      className={`bot-turn-card ${isSelected ? 'selected' : ''}`}
      style={{ borderLeftColor: config.color }}
      onClick={() => onSelectTurn(turn.turn)}
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
    >
      <div className="bot-turn-header">
        <span className="bot-turn-number">T{turn.turn}</span>
        <span className="bot-turn-player">{turn.playerName}</span>
        <ClassificationBadge classification={turn.classification} />
      </div>
      <div className="bot-turn-actions">
        <div className="bot-turn-actual">
          <span className="bot-turn-action-label">Played:</span>
          <span className="bot-turn-action-desc">{turn.actualAction}</span>
        </div>
        {turn.botRecommendation && turn.classification !== 'correct' && (
          <div className="bot-turn-recommended">
            <span className="bot-turn-action-label">Bot:</span>
            <span className="bot-turn-action-desc">{turn.botRecommendation}</span>
          </div>
        )}
      </div>
    </motion.div>
  )
}

function TurnDetail({ turn, suitNames }) {
  const [expandedCandidate, setExpandedCandidate] = useState(null)
  const [highlightedOrders, setHighlightedOrders] = useState([])

  const state = turn.state
  if (!state) return null

  return (
    <motion.div
      className="bot-turn-detail"
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
    >
      {/* Mini board state */}
      <div className="bot-state-bar">
        <span className="bot-state-item">Score: <strong>{state.score}/{state.maxScore}</strong></span>
        <span className="bot-state-item">Clues: <strong style={{ color: 'var(--cyan)' }}>{state.clueTokens}</strong></span>
        <span className="bot-state-item">Strikes: <strong style={{ color: state.strikes > 0 ? 'var(--rose)' : 'inherit' }}>{state.strikes}</strong></span>
        <span className="bot-state-item">Pace: <strong>{state.pace}</strong></span>
        <span className="bot-state-item">Deck: <strong>{state.cardsLeft}</strong></span>
      </div>

      {/* Play stacks */}
      <div className="bot-play-stacks">
        {state.playStacks?.map((value, suitIndex) => (
          <div
            key={suitIndex}
            className="bot-stack"
            style={{ '--suit-color': SUIT_COLORS[suitIndex] || '#888' }}
          >
            <span className="bot-stack-value">{value}</span>
          </div>
        ))}
      </div>

      {/* Hands */}
      <div className="bot-hands">
        {state.hands?.map((hand, playerIndex) => (
          <div key={playerIndex} className="bot-hand-row">
            <span className={`bot-hand-name ${playerIndex === state.currentPlayerIndex ? 'current' : ''}`}>
              {suitNames ? '' : ''}{state.suits ? '' : ''}{/* use playerIndex from state */}
              P{playerIndex + 1}
              {playerIndex === state.currentPlayerIndex && ' *'}
            </span>
            <div className="bot-hand-cards">
              {hand.map((card, i) => (
                <MiniHandCard
                  key={card.order ?? i}
                  card={card}
                  highlighted={highlightedOrders.includes(card.order)}
                  suitNames={suitNames}
                />
              ))}
            </div>
          </div>
        ))}
      </div>

      {/* Candidates table */}
      {turn.candidates && turn.candidates.length > 0 && (
        <div className="bot-candidates">
          <div className="bot-candidates-header">Candidates</div>
          {turn.candidates.map((c, i) => (
            <CandidateRow
              key={i}
              candidate={c}
              isExpanded={expandedCandidate === i}
              onToggle={() => setExpandedCandidate(expandedCandidate === i ? null : i)}
              highlightedCards={highlightedOrders}
              onHover={setHighlightedOrders}
            />
          ))}
        </div>
      )}
    </motion.div>
  )
}

function BotAnalysisPanel({ gameId }) {
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [selectedTurn, setSelectedTurn] = useState(null)
  const [filterPlayer, setFilterPlayer] = useState(null)
  const [isCollapsed, setIsCollapsed] = useState(false)

  useEffect(() => {
    if (!gameId) return
    setLoading(true)
    setError(null)
    setData(null)
    setSelectedTurn(null)

    fetch(`${BOT_API_URL}/api/review/${gameId}?level=5`)
      .then(res => {
        if (!res.ok) throw new Error(`Bot API returned ${res.status}`)
        return res.json()
      })
      .then(setData)
      .catch(err => setError(err.message))
      .finally(() => setLoading(false))
  }, [gameId])

  if (loading) {
    return (
      <motion.div
        className="bot-panel"
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.3 }}
      >
        <div className="bot-panel-header">
          <h3 className="bot-panel-title">Bot Analysis</h3>
        </div>
        <div className="bot-panel-loading">
          <div className="loading-spinner"></div>
          <p className="loading-text">Running bot analysis...</p>
          <p className="bot-loading-sub">This may take a moment for longer games</p>
        </div>
      </motion.div>
    )
  }

  if (error) {
    return (
      <motion.div
        className="bot-panel"
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.3 }}
      >
        <div className="bot-panel-header">
          <h3 className="bot-panel-title">Bot Analysis</h3>
        </div>
        <div className="bot-panel-error">
          <p>Could not load bot analysis</p>
          <p className="bot-error-detail">{error}</p>
        </div>
      </motion.div>
    )
  }

  if (!data) return null

  const { summary, turns, gameInfo } = data
  const suitNames = gameInfo.suits || []

  const filteredTurns = filterPlayer
    ? turns.filter(t => t.playerName === filterPlayer)
    : turns

  const selectedTurnData = turns.find(t => t.turn === selectedTurn)

  return (
    <motion.div
      className="bot-panel"
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: 0.3 }}
    >
      <div className="bot-panel-header" onClick={() => setIsCollapsed(!isCollapsed)}>
        <h3 className="bot-panel-title">
          Bot Analysis
          <span className="bot-panel-level">Level {gameInfo.level}</span>
        </h3>
        <div className="bot-panel-accuracy">
          <span className="bot-accuracy-pct">{summary.accuracy}%</span>
          <span className="bot-accuracy-label">accuracy</span>
        </div>
        <span className="bot-panel-collapse">{isCollapsed ? '+' : '−'}</span>
      </div>

      <AnimatePresence>
        {!isCollapsed && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.3 }}
            style={{ overflow: 'hidden' }}
          >
            {/* Overall accuracy bar */}
            <div className="bot-summary-section">
              <AccuracyBar classifications={summary.classifications} total={summary.totalTurns} />
              <div className="bot-classification-legend">
                {Object.entries(CLASSIFICATION_CONFIG).filter(([k]) => k !== 'unknown').map(([key, cfg]) => (
                  <span key={key} className="bot-legend-item">
                    <span className="bot-legend-dot" style={{ background: cfg.color }} />
                    {cfg.label}: {summary.classifications[key] || 0}
                  </span>
                ))}
              </div>
            </div>

            {/* Per-player accuracy */}
            <div className="bot-per-player">
              {Object.entries(summary.perPlayer).map(([name, stats]) => (
                <div
                  key={name}
                  className={`bot-player-row ${filterPlayer === name ? 'active' : ''}`}
                  onClick={() => setFilterPlayer(filterPlayer === name ? null : name)}
                >
                  <span className="bot-player-name">{name}</span>
                  <div className="bot-player-bar-container">
                    <AccuracyBar classifications={stats} total={stats.total} />
                  </div>
                  <span className="bot-player-pct">
                    {stats.total > 0 ? Math.round(((stats.correct + stats.good) / stats.total) * 100) : 0}%
                  </span>
                </div>
              ))}
            </div>

            {/* Turn list */}
            <div className="bot-turns-section">
              <div className="bot-turns-header">
                <span>Turn-by-Turn ({filteredTurns.length} turns)</span>
                {filterPlayer && (
                  <button className="bot-filter-clear" onClick={() => setFilterPlayer(null)}>
                    Clear filter
                  </button>
                )}
              </div>
              <div className="bot-turns-list">
                {filteredTurns.map(turn => (
                  <TurnCard
                    key={turn.turn}
                    turn={turn}
                    suitNames={suitNames}
                    onSelectTurn={setSelectedTurn}
                    isSelected={selectedTurn === turn.turn}
                  />
                ))}
              </div>
            </div>

            {/* Selected turn detail */}
            <AnimatePresence>
              {selectedTurnData && (
                <TurnDetail turn={selectedTurnData} suitNames={suitNames} />
              )}
            </AnimatePresence>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  )
}

export default BotAnalysisPanel
