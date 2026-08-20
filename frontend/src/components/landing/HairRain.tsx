'use client'

import { useEffect, useRef } from 'react'

/**
 * Fios de cabelo caindo pela luz — o instante logo depois de a máquina passar.
 *
 * Três camadas de profundidade: os fios do fundo são finos, lentos e apagados;
 * os da frente são grossos, rápidos e brilhantes. Um recorte radial no meio
 * apaga os fios atrás do texto, para o herói continuar legível.
 *
 * Dispare `imperador:snip` na window com `{ x, y }` para soltar uma lufada de
 * fios daquele ponto — é o que o botão de agendar faz ao ser pressionado.
 */

const GOLD = [
  [201, 168, 76], // brand-gold
  [232, 201, 106], // brand-gold-light
  [168, 135, 46], // brand-gold-dark
] as const

interface Strand {
  x: number
  y: number
  length: number
  /** Curvatura do fio: cabelo cortado cai torto, não reto. */
  bend: number
  angle: number
  spin: number
  vy: number
  vx: number
  /** 0 = fundo, 1 = frente. Comanda tamanho, brilho e velocidade. */
  depth: number
  width: number
  alpha: number
  color: readonly [number, number, number]
  swayPhase: number
  swayAmp: number
  /** Lufada do clique: some sozinha em vez de circular para sempre. */
  life: number
}

function randomColor() {
  return GOLD[Math.floor(Math.random() * GOLD.length)]
}

function spawn(width: number, height: number, atTop: boolean): Strand {
  const depth = Math.random()
  return {
    x: Math.random() * width,
    y: atTop ? -40 - Math.random() * height * 0.4 : Math.random() * height,
    length: 14 + depth * 30,
    bend: (Math.random() - 0.5) * 26,
    angle: Math.random() * Math.PI,
    spin: (Math.random() - 0.5) * 0.012,
    vy: 12 + depth * 34,
    vx: (Math.random() - 0.5) * 6,
    depth,
    width: 0.6 + depth * 1.5,
    alpha: 0.14 + depth * 0.34,
    color: randomColor(),
    swayPhase: Math.random() * Math.PI * 2,
    swayAmp: 6 + depth * 16,
    life: 1,
  }
}

function burst(x: number, y: number): Strand[] {
  return Array.from({ length: 14 }, () => {
    const depth = 0.45 + Math.random() * 0.55
    const spread = (Math.random() - 0.5) * Math.PI * 1.1
    const speed = 40 + Math.random() * 90
    return {
      x,
      y,
      length: 12 + depth * 22,
      bend: (Math.random() - 0.5) * 30,
      angle: Math.random() * Math.PI,
      spin: (Math.random() - 0.5) * 0.09,
      vy: Math.sin(spread) * speed * 0.4 + 30,
      vx: Math.cos(spread) * speed,
      depth,
      width: 0.8 + depth * 1.4,
      alpha: 0.35 + depth * 0.35,
      color: randomColor(),
      swayPhase: Math.random() * Math.PI * 2,
      swayAmp: 4 + depth * 8,
      life: 1,
    }
  })
}

function drawStrand(ctx: CanvasRenderingContext2D, s: Strand, time: number) {
  const sway = Math.sin(time * 0.0009 + s.swayPhase) * s.swayAmp
  const half = s.length / 2

  ctx.save()
  ctx.translate(s.x + sway, s.y)
  ctx.rotate(s.angle)

  const [r, g, b] = s.color
  ctx.strokeStyle = `rgba(${r}, ${g}, ${b}, ${s.alpha * s.life})`
  ctx.lineWidth = s.width
  ctx.lineCap = 'round'

  ctx.beginPath()
  ctx.moveTo(0, -half)
  ctx.quadraticCurveTo(s.bend, 0, 0, half)
  ctx.stroke()
  ctx.restore()
}

export function HairRain() {
  const canvasRef = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d', { alpha: true })
    if (!ctx) return

    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)')
    let strands: Strand[] = []
    let width = 0
    let height = 0
    let frame = 0
    let last = 0
    let running = false

    function resize() {
      if (!canvas || !ctx) return
      const rect = canvas.getBoundingClientRect()
      width = rect.width
      height = rect.height
      // DPR travado em 2: acima disso o custo por pixel não paga a diferença
      // num Android de entrada.
      const dpr = Math.min(window.devicePixelRatio || 1, 2)
      canvas.width = Math.round(width * dpr)
      canvas.height = Math.round(height * dpr)
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0)

      const target = Math.round(
        Math.min(84, Math.max(26, (width * height) / 15000))
      )
      strands = Array.from({ length: target }, () => spawn(width, height, false))
    }

    /** Apaga o que caiu atrás do texto do herói. */
    function maskCenter() {
      if (!ctx) return
      const gradient = ctx.createRadialGradient(
        width / 2,
        height * 0.46,
        0,
        width / 2,
        height * 0.46,
        Math.min(width, height) * 0.52
      )
      gradient.addColorStop(0, 'rgba(0,0,0,0.92)')
      gradient.addColorStop(0.55, 'rgba(0,0,0,0.55)')
      gradient.addColorStop(1, 'rgba(0,0,0,0)')
      ctx.globalCompositeOperation = 'destination-out'
      ctx.fillStyle = gradient
      ctx.fillRect(0, 0, width, height)
      ctx.globalCompositeOperation = 'source-over'
    }

    function renderStatic() {
      if (!ctx) return
      ctx.clearRect(0, 0, width, height)
      strands.forEach((s) => drawStrand(ctx, s, 0))
      maskCenter()
    }

    function tick(now: number) {
      if (!ctx) return
      const dt = Math.min((now - last) / 1000, 0.05)
      last = now

      ctx.clearRect(0, 0, width, height)

      for (let i = strands.length - 1; i >= 0; i--) {
        const s = strands[i]
        s.y += s.vy * dt
        s.x += s.vx * dt
        s.angle += s.spin
        if (s.vx !== 0) s.vx *= 0.985

        if (s.life < 1 || s.vx > 12 || s.vx < -12) {
          s.life -= dt * 0.55
          if (s.life <= 0) {
            strands.splice(i, 1)
            continue
          }
        }

        if (s.y - s.length > height) {
          strands[i] = spawn(width, height, true)
        }
        drawStrand(ctx, s, now)
      }

      maskCenter()
      frame = requestAnimationFrame(tick)
    }

    function start() {
      if (running || reduced.matches) return
      running = true
      last = performance.now()
      frame = requestAnimationFrame(tick)
    }

    function stop() {
      running = false
      cancelAnimationFrame(frame)
    }

    function handleSnip(event: Event) {
      if (reduced.matches || !canvas) return
      const { x, y } = (event as CustomEvent<{ x: number; y: number }>).detail ?? {}
      if (typeof x !== 'number' || typeof y !== 'number') return
      const rect = canvas.getBoundingClientRect()
      // O corte acontece onde o dedo tocou, não no meio da tela.
      strands.push(...burst(x - rect.left, y - rect.top))
    }

    function handleVisibility() {
      if (document.hidden) stop()
      else if (visible) start()
    }

    function handleMotionPreference() {
      stop()
      resize()
      if (reduced.matches) renderStatic()
      else if (visible) start()
    }

    let visible = true
    const observer = new IntersectionObserver(
      ([entry]) => {
        visible = entry.isIntersecting
        // Loop decorativo não roda fora da tela.
        if (!visible) stop()
        else if (!reduced.matches && !document.hidden) start()
      },
      { threshold: 0 }
    )

    resize()
    observer.observe(canvas)
    if (reduced.matches) renderStatic()
    else start()

    const resizeObserver = new ResizeObserver(() => {
      resize()
      if (reduced.matches) renderStatic()
    })
    resizeObserver.observe(canvas)

    window.addEventListener('imperador:snip', handleSnip)
    document.addEventListener('visibilitychange', handleVisibility)
    reduced.addEventListener('change', handleMotionPreference)

    return () => {
      stop()
      observer.disconnect()
      resizeObserver.disconnect()
      window.removeEventListener('imperador:snip', handleSnip)
      document.removeEventListener('visibilitychange', handleVisibility)
      reduced.removeEventListener('change', handleMotionPreference)
    }
  }, [])

  return (
    <canvas
      ref={canvasRef}
      aria-hidden="true"
      className="pointer-events-none absolute inset-0 h-full w-full"
    />
  )
}
