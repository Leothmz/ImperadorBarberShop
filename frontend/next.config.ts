import type { NextConfig } from "next";

const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

const nextConfig: NextConfig = {
  // Login, refresh e logout passam pela origem do próprio site. A API responde com o
  // refresh token num cookie HttpOnly; chamando a API direto, o cookie ficaria gravado
  // no host dela (api.…) e o middleware, que roda neste host, nunca o enxergaria.
  async rewrites() {
    return [{ source: '/api/v1/auth/:path*', destination: `${apiUrl}/api/v1/auth/:path*` }]
  },

  // Sem isto o Next 16 bloqueia os assets de dev quando a página é aberta por IP
  // da rede local (teste no celular): o HMR cai e o React não hidrata.
  // Só vale em dev; ignorado no build de produção.
  allowedDevOrigins: ['192.168.1.104'],

  images: {
    // As fotos de barbeiro e serviço são hospedadas no Cloudinary. Sem declarar
    // o host aqui, o next/image recusa a URL em runtime — a foto só falharia
    // quando alguém finalmente subisse uma.
    remotePatterns: [{ protocol: 'https', hostname: 'res.cloudinary.com' }],
  },
};

export default nextConfig;
