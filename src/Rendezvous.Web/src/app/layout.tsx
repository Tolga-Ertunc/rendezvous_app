import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Rendezvous",
  description: "Appointment booking platform for businesses and customers.",
  icons: {
    icon: [
      {
        url: "/rendezvous-logo.png",
        sizes: "500x500",
        type: "image/png",
      },
    ],
    shortcut: "/favicon.ico",
    apple: "/rendezvous-logo.png",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="h-full antialiased">
      <body className="min-h-full flex flex-col">{children}</body>
    </html>
  );
}
