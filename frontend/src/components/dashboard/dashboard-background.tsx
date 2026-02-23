'use client';

export function DashboardBackground() {
  return (
    <div aria-hidden className="pointer-events-none fixed inset-0 z-0 overflow-hidden">
      <div className="absolute -left-32 -top-40 h-[600px] w-[600px] rounded-full bg-violet-100/40 blur-[160px]" />
      <div className="absolute -bottom-48 right-[-10%] h-[500px] w-[500px] rounded-full bg-fuchsia-100/25 blur-[140px]" />
    </div>
  );
}
