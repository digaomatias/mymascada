'use client';

export function DashboardBackground() {
  return (
    <div aria-hidden className="pointer-events-none fixed inset-0 z-0 overflow-hidden">
      {/* Top-left: warm violet wash */}
      <div
        className="absolute -left-32 -top-40 h-[550px] w-[550px] rounded-full blur-[160px]"
        style={{ background: 'oklch(92% 0.04 285 / 35%)' }}
      />
      {/* Bottom-right: subtle rose/fuchsia accent */}
      <div
        className="absolute -bottom-48 right-[-8%] h-[450px] w-[450px] rounded-full blur-[140px]"
        style={{ background: 'oklch(93% 0.03 320 / 20%)' }}
      />
    </div>
  );
}
