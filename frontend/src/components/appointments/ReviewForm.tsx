'use client'

import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { StarRatingInput } from '@/components/ui/StarRating'
import { reviewsApi } from '@/lib/api/reviews.api'

interface ReviewFormProps {
  appointmentId: string
  onSuccess: () => void
}

export function ReviewForm({ appointmentId, onSuccess }: ReviewFormProps) {
  const [rating, setRating] = useState(0)
  const [comment, setComment] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (rating === 0) {
      setError('Selecione uma avaliação de 1 a 5 estrelas.')
      return
    }
    setError(null)
    setIsSubmitting(true)
    try {
      await reviewsApi.create({
        appointmentId,
        rating,
        comment: comment.trim() || undefined,
      })
      onSuccess()
    } catch {
      setError('Erro ao enviar avaliação. Tente novamente.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <div className="flex flex-col items-center gap-2">
        <p className="text-sm text-brand-white/70">Como você avalia o serviço?</p>
        <StarRatingInput value={rating} onChange={setRating} />
        {rating > 0 && (
          <span className="text-xs text-brand-gold">
            {['', 'Péssimo', 'Ruim', 'Regular', 'Bom', 'Excelente'][rating]}
          </span>
        )}
      </div>

      <div className="flex flex-col gap-1">
        <label
          htmlFor="review-comment"
          className="text-sm font-medium text-brand-white/80"
        >
          Comentário (opcional)
        </label>
        <textarea
          id="review-comment"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          placeholder="Conte como foi sua experiência..."
          rows={3}
          className="w-full rounded-md border border-brand-white/20 bg-brand-black px-3 py-2.5 text-brand-white placeholder:text-brand-white/30 focus:border-brand-gold focus:outline-none focus:ring-1 focus:ring-brand-gold resize-none"
        />
      </div>

      {error && (
        <p role="alert" className="text-sm text-red-400">
          {error}
        </p>
      )}

      <Button type="submit" isLoading={isSubmitting} className="w-full">
        Enviar avaliação
      </Button>
    </form>
  )
}
