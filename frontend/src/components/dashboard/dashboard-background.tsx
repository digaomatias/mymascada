'use client';

export function DashboardBackground() {
  return (
    <div aria-hidden className="pointer-events-none fixed inset-0 z-0 overflow-hidden">
      {/* Top-left: subtle teal wash */}
      <div
        className="absolute -left-32 -top-40 h-[550px] w-[550px] rounded-full blur-[160px]"
        style={{ background: 'oklch(93% 0.035 168 / 25%)' }}
      />
      {/* Bottom-right: warm amber accent */}
      <div
        className="absolute -bottom-48 right-[-8%] h-[450px] w-[450px] rounded-full blur-[140px]"
        style={{ background: 'oklch(94% 0.025 55 / 20%)' }}
      />
    </div>
  );
}
