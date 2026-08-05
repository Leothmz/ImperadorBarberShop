import type { Metadata } from 'next'
import { Montserrat, Inter } from 'next/font/google'
import './globals.css'
import { AuthProvider } from '@/providers/AuthProvider'
import { QueryProvider } from '@/providers/QueryProvider'
import { Header } from '@/components/layout/Header'
import { Footer } from '@/components/layout/Footer'

const montserrat = Montserrat({
  subsets: ['latin'],
  variable: '--font-montserrat',
  display: 'swap',
})

const inter = Inter({
  subsets: ['latin'],
  variable: '--font-inter',
  display: 'swap',
})

// Ícones vêm de src/app/icon.png e src/app/apple-icon.png (convenção de arquivo do Next)
export const metadata: Metadata = {
  title: 'O Imperador Barber Shop',
  description: 'Agende seu corte com os melhores barbeiros da cidade.',
  // Nome curto no ícone da tela de início; o title completo corta no iOS
  appleWebApp: { title: 'OImperador' },
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <html lang="pt-BR" className={`${montserrat.variable} ${inter.variable}`}>
      <body className="min-h-screen flex flex-col bg-brand-black text-brand-white antialiased" suppressHydrationWarning>
        <QueryProvider>
          <AuthProvider>
            <Header />
            <main className="flex-1">{children}</main>
            <Footer />
          </AuthProvider>
        </QueryProvider>
      </body>
    </html>
  )
}
