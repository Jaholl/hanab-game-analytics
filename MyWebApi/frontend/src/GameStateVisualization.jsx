import { useState, useRef, useCallback, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { motion, animate } from 'motion/react'

const SUIT_NAMES = ['Red', 'Yellow', 'Green', 'Blue', 'Purple']
const SUIT_COLORS = ['#ff4444', '#ffdd44', '#44dd44', '#4488ff', '#aa44ff']
const SUIT_ABBREVIATIONS = ['R', 'Y', 'G', 'B', 'P']

function CardAnimationOverlay({ animationState, onComplete }) {
  const overlayRef = useRef(null)
  const onCompleteRef = useRef(onComplete)
  onCompleteRef.current = onComplete

  useEffect(() => {
    if (!animationState || !overlayRef.current) return

    let cancelled = false
    const el = overlayRef.current
    const { sourceRect, targetRect, intermediateRect, type } = animationState

    // Compute deltas for transform-based animation (x/y are GPU-accelerated)
    const dx = targetRect.left - sourceRect.left
    const dy = targetRect.top - sourceRect.top
    const intDx = intermediateRect ? intermediateRect.left - sourceRect.left : 0
    const intDy = intermediateRect ? intermediateRect.top - sourceRect.top : 0

    const runAnimation = async () => {
      try {
        if (type === 'play') {
          await animate(el, {
            x: [0, dx],
            y: [0, dy],
            scale: [1, 1.1, 1],
          }, { duration: 0.4, ease: 'easeOut' }).finished

        } else if (type === 'discard') {
          await animate(el, {
            x: [0, dx],
            y: [0, dy],
            scale: [1, 0.9],
            opacity: [1, 0.7],
          }, { duration: 0.35, ease: 'easeIn' }).finished

        } else if (type === 'misplay') {
          // Step 1: Fly to play stack
          await animate(el, {
            x: [0, intDx],
            y: [0, intDy],
          }, { duration: 0.3, ease: 'easeOut' }).finished

          if (cancelled) return

          // Step 2: Shake + red flash
          el.style.boxShadow = '0 0 20px rgba(255, 0, 110, 0.8), 0 0 40px rgba(255, 0, 110, 0.4)'
          await animate(el, {
            x: [intDx, intDx - 8, intDx + 8, intDx - 6, intDx + 6, intDx - 3, intDx + 3, intDx],
          }, { duration: 0.4, ease: 'easeInOut' }).finished

          if (cancelled) return
          el.style.boxShadow = ''

          // Step 3: Fly to trash
          await animate(el, {
            x: [intDx, dx],
            y: [intDy, dy],
            opacity: [1, 0.7],
          }, { duration: 0.3, ease: 'easeIn' }).finished
        }
      } catch {
        // Animation interrupted
      }
      if (!cancelled) {
        onCompleteRef.current()
      }
    }

    runAnimation()

    return () => { cancelled = true }
  }, [animationState])

  if (!animationState) return null

  const { card, sourceRect } = animationState
  const suitColor = SUIT_COLORS[card.suitIndex]

  return createPortal(
    <div
      ref={overlayRef}
      className="card-animation-overlay hand-card"
      style={{
        '--suit-color': suitColor,
        position: 'fixed',
        left: sourceRect.left,
        top: sourceRect.top,
        width: sourceRect.width,
        height: sourceRect.height,
        zIndex: 10000,
        pointerEvents: 'none',
      }}
    >
      <div className="card-face">
        <span className="card-suit">{SUIT_ABBREVIATIONS[card.suitIndex]}</span>
        <span className="card-rank">{card.rank}</span>
      </div>
    </div>,
    document.body
  )
}

function GameStateVisualization({ state, nextState, currentAction, highlightedDeckIndex, players, currentPlayerOverride, previousAction, currentTurn, violationTurn, minTurn, maxTurn, onPrevTurn, onNextTurn }) {
  const [animationState, setAnimationState] = useState(null)
  const containerRef = useRef(null)
  const trashPileRef = useRef(null)

  if (!state) {
    return (
      <div className="game-state-unavailable">
        State unavailable
      </div>
    )
  }

  const currentPlayer = currentPlayerOverride !== undefined ? currentPlayerOverride : state.currentPlayer

  const prevClue = previousAction && (previousAction.type === 2 || previousAction.type === 3)
    ? {
        targetPlayer: previousAction.target,
        isColor: previousAction.type === 2,
        value: previousAction.value
      }
    : null

  const wasCardClued = (card, playerIndex) => {
    if (!prevClue || playerIndex !== prevClue.targetPlayer) return false
    if (prevClue.isColor) {
      return card.suitIndex === prevClue.value
    } else {
      return card.rank === prevClue.value
    }
  }

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

  const handleNextTurn = useCallback(() => {
    // If no current action or it's a clue, skip animation
    if (!currentAction || currentAction.type === 2 || currentAction.type === 3) {
      onNextTurn()
      return
    }

    // Check prefers-reduced-motion
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
      onNextTurn()
      return
    }

    // Need both states to figure out which card moved
    if (!nextState || !containerRef.current) {
      onNextTurn()
      return
    }

    // Find the card that was removed from a hand
    // Compare current hands vs next hands to find the missing card
    let movedCard = null
    for (let pi = 0; pi < state.hands.length; pi++) {
      const currentHand = state.hands[pi]
      const nextHand = nextState.hands[pi] || []
      const nextDeckIndices = new Set(nextHand.map(c => c.deckIndex))
      for (const card of currentHand) {
        if (!nextDeckIndices.has(card.deckIndex)) {
          movedCard = card
          break
        }
      }
      if (movedCard) break
    }

    if (!movedCard) {
      onNextTurn()
      return
    }

    // Measure source position
    const sourceEl = containerRef.current.querySelector(`[data-deck-index="${movedCard.deckIndex}"]`)
    if (!sourceEl) {
      onNextTurn()
      return
    }
    const sourceRect = sourceEl.getBoundingClientRect()

    // Determine animation type and target
    const isMisplay = nextState.strikes > state.strikes
    const isPlay = currentAction.type === 0
    const isDiscard = currentAction.type === 1

    let targetRect
    let intermediateRect = null
    let animationType

    if (isPlay && !isMisplay) {
      // Successful play → fly to the correct suit stack
      animationType = 'play'
      const stackEl = containerRef.current.querySelector(`[data-suit-index="${movedCard.suitIndex}"]`)
      if (!stackEl) { onNextTurn(); return }
      targetRect = stackEl.getBoundingClientRect()
    } else if (isPlay && isMisplay) {
      // Misplay → fly to stack, shake, fly to trash
      animationType = 'misplay'
      const stackEl = containerRef.current.querySelector(`[data-suit-index="${movedCard.suitIndex}"]`)
      if (!stackEl) { onNextTurn(); return }
      intermediateRect = stackEl.getBoundingClientRect()
      // Target is trash pile
      if (!trashPileRef.current) { onNextTurn(); return }
      targetRect = trashPileRef.current.getBoundingClientRect()
    } else if (isDiscard) {
      // Discard → fly to trash
      animationType = 'discard'
      if (!trashPileRef.current) { onNextTurn(); return }
      targetRect = trashPileRef.current.getBoundingClientRect()
    } else {
      onNextTurn()
      return
    }

    setAnimationState({
      card: movedCard,
      sourceRect,
      targetRect,
      intermediateRect,
      type: animationType,
      hiddenDeckIndex: movedCard.deckIndex,
    })
  }, [currentAction, nextState, state, onNextTurn])

  const handleAnimationComplete = useCallback(() => {
    setAnimationState(null)
    onNextTurn()
  }, [onNextTurn])

  const isAnimating = animationState !== null
  const discardPile = state.discardPile || []

  return (
    <motion.div
      className="game-state-visualization"
      initial={{ opacity: 0, height: 0 }}
      animate={{ opacity: 1, height: 'auto' }}
      exit={{ opacity: 0, height: 0 }}
      transition={{ duration: 0.3 }}
      ref={containerRef}
    >
      {/* Turn Navigation */}
      {violationTurn && maxTurn > minTurn && (
        <div className="turn-nav-bar" onClick={e => e.stopPropagation()}>
          <button
            className="turn-nav-btn"
            disabled={currentTurn <= minTurn || isAnimating}
            onClick={onPrevTurn}
          >
            &lt; Prev
          </button>
          <span className={`turn-nav-indicator ${currentTurn !== violationTurn ? 'viewing-history' : ''}`}>
            Turn {currentTurn}{currentTurn !== violationTurn ? ` (violation: ${violationTurn})` : ''}
          </span>
          <button
            className="turn-nav-btn"
            disabled={currentTurn >= maxTurn || isAnimating}
            onClick={handleNextTurn}
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

      {/* Play Stacks & Trash */}
      <div className="state-section">
        <div className="stacks-and-trash">
          <div className="stacks-column">
            <div className="state-section-title">Play Stacks</div>
            <div className="play-stacks">
              {state.playStacks.map((value, suitIndex) => (
                <div
                  key={suitIndex}
                  className="play-stack"
                  data-suit-index={suitIndex}
                  style={{ '--suit-color': SUIT_COLORS[suitIndex] }}
                >
                  <div className="stack-suit">{SUIT_ABBREVIATIONS[suitIndex]}</div>
                  <div className="stack-value">{value}</div>
                </div>
              ))}
            </div>
          </div>
          <div className="trash-column">
            <div className="state-section-title">Trash ({discardPile.length})</div>
            <div className="trash-pile" ref={trashPileRef}>
              {discardPile.map((card, i) => (
                <div
                  key={i}
                  className="trash-card"
                  style={{ '--suit-color': SUIT_COLORS[card.suitIndex] }}
                >
                  <span className="card-rank">{card.rank}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Player Hands */}
      <div className="state-section">
        <div className="state-section-title">Player Hands (Omniscient View)</div>
        <div className="hand-order-legend">
          <span className="legend-newest">newest</span>
          <span className="legend-arrow">→</span>
          <span className="legend-oldest">chop</span>
        </div>
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
                {[...hand].reverse().map((card) => {
                  const isHighlighted = card.deckIndex === highlightedDeckIndex
                  const wasClued = wasCardClued(card, playerIndex)
                  const isHidden = animationState?.hiddenDeckIndex === card.deckIndex
                  return (
                    <div
                      key={card.deckIndex}
                      data-deck-index={card.deckIndex}
                      className={`hand-card ${isHighlighted ? 'highlighted-card' : ''} ${wasClued ? 'just-clued' : ''} ${card.hasAnyClue ? 'has-clues' : ''} ${isHidden ? 'animating-out' : ''}`}
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

      {/* Card Animation Overlay */}
      <CardAnimationOverlay
        animationState={animationState}
        onComplete={handleAnimationComplete}
      />

    </motion.div>
  )
}

export default GameStateVisualization
