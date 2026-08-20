/**
 * Três passos numa régua dourada. Os números ficam porque a ordem é a
 * informação — não são enfeite de seção.
 */
const STEPS = [
  {
    title: 'Escolha o que quer fazer',
    body: 'Barbeiro, serviços e horário. A agenda soma a duração do que você escolheu e mostra só os horários que cabem.',
  },
  {
    title: 'Confirme com nome e WhatsApp',
    body: 'É só isso. Sem cadastro, sem senha, sem app para instalar.',
  },
  {
    title: 'Guarde o link',
    body: 'Ele é o seu acesso ao agendamento: dá para cancelar por ele até 2 horas antes e avaliar o corte depois.',
  },
]

export function HowItWorks() {
  return (
    <section className="border-t border-brand-white/10 px-4 py-20 sm:px-6">
      <div className="mx-auto max-w-5xl">
        <h2 className="font-montserrat text-3xl font-black tracking-tight text-brand-white sm:text-4xl">
          Como funciona
        </h2>

        <ol className="relative mt-10 grid gap-10 sm:grid-cols-3 sm:gap-8">
          {/* A régua que liga os passos: vertical no celular, horizontal no desktop */}
          <span
            aria-hidden="true"
            className="absolute top-0 bottom-0 left-[19px] w-px bg-gradient-to-b from-brand-gold/40 via-brand-gold/20 to-transparent sm:top-[19px] sm:right-0 sm:bottom-auto sm:left-0 sm:h-px sm:w-auto sm:bg-gradient-to-r"
          />

          {STEPS.map((step, index) => (
            <li key={step.title} className="relative flex gap-4 sm:flex-col">
              <span className="relative z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full border border-brand-gold/40 bg-brand-black font-montserrat text-sm font-bold text-brand-gold">
                {index + 1}
              </span>
              <div className="sm:pr-4">
                <h3 className="font-montserrat text-lg font-bold text-brand-white">
                  {step.title}
                </h3>
                <p className="mt-2 text-brand-white/60">{step.body}</p>
              </div>
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}
