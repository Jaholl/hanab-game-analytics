import { useState } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import GameStateVisualization from './GameStateVisualization'

const SUIT_NAMES = ['Red', 'Yellow', 'Green', 'Blue', 'Purple']
const SUIT_COLORS = ['#ff4444', '#ffdd44', '#44dd44', '#4488ff', '#aa44ff']

const ACTION_NAMES = {
  0: 'Play',
  1: 'Discard',
  2: 'Color Clue',
  3: 'Rank Clue'
}

const getSeverityClass = (severity) => {
  switch (severity) {
    case 'critical': return 'severity-critical'
    case 'warning': return 'severity-warning'
    case 'info': return 'severity-info'
    default: return ''
  }
}

const getSeverityIcon = (severity) => {
  switch (severity) {
    case 'critical': return '!!'
    case 'warning': return '!'
    case 'info': return 'i'
    default: return '?'
  }
}

const formatViolationType = (type) => {
  switch (type) {
    // Phase 1
    case 'Misplay': return 'Misplay'
    case 'BadDiscard5': return 'Discarded 5'
    case 'BadDiscardCritical': return 'Discarded Critical'
    // Phase 2
    case 'GoodTouchViolation': return 'Good Touch'
    case 'MCVPViolation': return 'MCVP'
    case 'MissedSave': return 'Missed Save'
    // Phase 3
    case 'MissedPrompt': return 'Missed Prompt'
    case 'MissedFinesse': return 'Missed Finesse'
    case 'BrokenFinesse': return 'Broken Finesse'
    default: return type
  }
}

function GameDetail({ gameId, analysis, loading, error, onBack }) {
  const [expandedViolation, setExpandedViolation] = useState(null)
  const [viewingTurn, setViewingTurn] = useState(null)

  // Get state BEFORE an action at the given turn
  // turn is 1-indexed (turn 1 = first action)
  // states array is 0-indexed: states[0] = initial deal, states[n] = after action n
  // To show state BEFORE the action: states[turn - 1]
  const getStateForTurn = (turn) => {
    const states = analysis?.states
    if (!states || turn < 1 || turn > states.length) return null
    return states[turn - 1]
  }

  const handleViolationClick = (index, violation) => {
    if (violation.severity === 'info') return
    if (expandedViolation === index) {
      setExpandedViolation(null)
      setViewingTurn(null)
    } else {
      setExpandedViolation(index)
      setViewingTurn(violation.turn)
    }
  }
  if (loading) {
    return (
      <div className="game-detail">
        <motion.div
          className="detail-loading"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
        >
          <div className="loading-spinner"></div>
          <p className="loading-text">Analyzing game...</p>
          <div className="skeleton-cards">
            <div className="skeleton-card"></div>
            <div className="skeleton-card"></div>
            <div className="skeleton-card"></div>
          </div>
        </motion.div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="game-detail">
        <motion.div
          className="detail-error"
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
        >
          <div className="error-icon">!!</div>
          <p className="error-title">Error Loading Game</p>
          <p className="error-message">{error}</p>
          <button className="back-btn" onClick={onBack}>
            Back to Games
          </button>
        </motion.div>
      </div>
    )
  }

  if (!analysis) return null

  const { game, violations, summary, variantSupported, variantName } = analysis

  // Variant not supported
  if (!variantSupported) {
    return (
      <div className="game-detail">
        <motion.div
          className="detail-header"
          initial={{ opacity: 0, y: -20 }}
          animate={{ opacity: 1, y: 0 }}
        >
          <button className="back-btn" onClick={onBack}>
            Back
          </button>
          <h2 className="detail-title">Game #{game.id}</h2>
        </motion.div>

        <motion.div
          className="variant-unsupported"
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ delay: 0.1 }}
        >
          <div className="unsupported-icon">?</div>
          <p className="unsupported-title">Variant Not Supported</p>
          <p className="unsupported-message">
            This game uses the <strong>{variantName}</strong> variant.
            Analysis only supports standard 5-suit games.
          </p>
          <button className="back-btn" onClick={onBack}>
            Back to Games
          </button>
        </motion.div>
      </div>
    )
  }

  const finalScore = game.actions?.length > 0
    ? analysis.game.deck ? calculateFinalScore(game) : 0
    : 0

  return (
    <div className="game-detail">
      {/* Header */}
      <motion.div
        className="detail-header"
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
      >
        <button className="back-btn" onClick={onBack}>
          Back
        </button>
        <div className="detail-title-section">
          <h2 className="detail-title">Game #{game.id}</h2>
          <span className="detail-meta">{game.players?.length || 0} players</span>
        </div>
      </motion.div>

      {/* Players */}
      <motion.div
        className="detail-players"
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.1 }}
      >
        {game.players?.map((player, i) => (
          <span key={i} className="player-badge">
            {player}
          </span>
        ))}
      </motion.div>

      {/* Violations Summary */}
      {violations && violations.length > 0 ? (
        <motion.div
          className="violations-panel"
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
        >
          <div className="violations-header">
            <h3 className="violations-title">
              Rule Violations ({summary.totalViolations})
            </h3>
            <div className="violations-summary">
              {summary.bySeverity?.critical > 0 && (
                <span className="summary-badge severity-critical">
                  {summary.bySeverity.critical} Critical
                </span>
              )}
              {summary.bySeverity?.warning > 0 && (
                <span className="summary-badge severity-warning">
                  {summary.bySeverity.warning} Warnings
                </span>
              )}
              {summary.bySeverity?.info > 0 && (
                <span className="summary-badge severity-info">
                  {summary.bySeverity.info} Info
                </span>
              )}
            </div>
          </div>

          <div className="violations-list">
            {violations.map((violation, i) => {
              const isExpandable = violation.severity !== 'info'
              const isCritical = violation.severity === 'critical'
              const isExpanded = expandedViolation === i
              const activeTurn = isExpanded ? viewingTurn : null
              const state = activeTurn ? getStateForTurn(activeTurn) : null
              const minTurn = Math.max(1, violation.turn - 5)
              const maxTurn = Math.min(game.actions?.length || violation.turn, violation.turn + 5)

              // Get previous action for the currently viewed turn
              const prevActionIndex = activeTurn ? activeTurn - 2 : violation.turn - 2
              const prevAction = prevActionIndex >= 0 ? game.actions[prevActionIndex] : null

              return (
                <motion.div
                  key={i}
                  className={`violation-card ${getSeverityClass(violation.severity)} ${isExpandable ? 'expandable' : ''} ${isExpanded ? 'expanded' : ''}`}
                  initial={{ opacity: 0, x: -20 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ delay: 0.3 + i * 0.05 }}
                  onClick={() => handleViolationClick(i, violation)}
                >
                  <div className="violation-header">
                    <span className={`violation-severity ${getSeverityClass(violation.severity)}`}>
                      {getSeverityIcon(violation.severity)}
                    </span>
                    <span className="violation-turn">Turn {violation.turn}</span>
                    <span className="violation-player">{violation.player}</span>
                    <span className="violation-type">{formatViolationType(violation.type)}</span>
                    {isExpandable && (
                      <span className="violation-expand-indicator">
                        {isExpanded ? '−' : '+'}
                      </span>
                    )}
                  </div>
                  <p className="violation-description">{violation.description}</p>
                  <AnimatePresence>
                    {isExpanded && (
                      <GameStateVisualization
                        state={state}
                        nextState={activeTurn ? getStateForTurn(activeTurn + 1) : null}
                        currentAction={activeTurn && activeTurn <= (game.actions?.length || 0) ? game.actions[activeTurn - 1] : null}
                        highlightedDeckIndex={activeTurn === violation.turn ? violation.card?.deckIndex : null}
                        players={game.players}
                        currentPlayerOverride={(activeTurn - 1) % game.players.length}
                        previousAction={prevAction}
                        currentTurn={activeTurn}
                        violationTurn={violation.turn}
                        minTurn={minTurn}
                        maxTurn={maxTurn}
                        onPrevTurn={() => setViewingTurn(t => Math.max(minTurn, t - 1))}
                        onNextTurn={() => setViewingTurn(t => Math.min(maxTurn, t + 1))}
                      />
                    )}
                  </AnimatePresence>
                </motion.div>
              )
            })}
          </div>
        </motion.div>
      ) : (
        <motion.div
          className="no-violations"
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
        >
          <span className="check-icon">OK</span>
          <p>No rule violations detected!</p>
          <p className="no-violations-subtitle">Great teamwork!</p>
        </motion.div>
      )}

      {/* Action Timeline */}
      <motion.div
        className="action-timeline"
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.4 }}
      >
        <details className="timeline-details">
          <summary className="timeline-summary">
            Action Timeline ({game.actions?.length || 0} actions)
          </summary>
          <div className="timeline-content">
            {game.actions?.map((action, i) => {
              const playerIndex = i % game.players.length
              const player = game.players[playerIndex]
              const hasViolation = violations?.some(v => v.turn === i + 1)

              return (
                <div
                  key={i}
                  className={`timeline-action ${hasViolation ? 'has-violation' : ''}`}
                >
                  <span className="action-turn">{i + 1}</span>
                  <span className="action-player">{player}</span>
                  <span className="action-type">{ACTION_NAMES[action.type] || 'Unknown'}</span>
                  {action.type <= 1 && (
                    <span className="action-target">slot {action.target + 1}</span>
                  )}
                  {action.type === 2 && (
                    <span className="action-target" style={{ color: SUIT_COLORS[action.value] }}>
                      {SUIT_NAMES[action.value]} to P{action.target + 1}
                    </span>
                  )}
                  {action.type === 3 && (
                    <span className="action-target">
                      {action.value} to P{action.target + 1}
                    </span>
                  )}
                  {hasViolation && <span className="violation-marker">!!</span>}
                </div>
              )
            })}
          </div>
        </details>
      </motion.div>
    </div>
  )
}

function calculateFinalScore(game) {
  // This is a simplified calculation - actual score comes from the simulation
  return 0
}

export default GameDetail
