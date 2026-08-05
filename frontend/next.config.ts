import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Sem isto o Next 16 bloqueia os assets de dev quando a página é aberta por IP
  // da rede local (teste no celular): o HMR cai e o React não hidrata.
  // Só vale em dev; ignorado no build de produção.
  allowedDevOrigins: ['192.168.1.104'],
};

export default nextConfig;
