import type { NextConfig } from "next";

const apiBaseUrl = process.env.RENDEZVOUS_API_BASE_URL ?? "http://localhost:5000";

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/backend-api/:path*",
        destination: `${apiBaseUrl}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;
