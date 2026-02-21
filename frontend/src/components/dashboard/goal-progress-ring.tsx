'use client';

interface GoalProgressRingProps {
  percentage: number;
  size?: number;
  strokeWidth?: number;
  color?: string;
}

const MILESTONES = [25, 50, 75, 100];

export function GoalProgressRing({
  percentage,
  size = 120,
  strokeWidth = 8,
  color = '#6366f1',
}: GoalProgressRingProps) {
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const clampedPercentage = Math.min(Math.max(percentage, 0), 100);
  const offset = circumference - (clampedPercentage / 100) * circumference;
  const center = size / 2;

  return (
    <svg
      width={size}
      height={size}
      viewBox={`0 0 ${size} ${size}`}
      className="transform -rotate-90"
    >
      {/* Background track */}
      <circle
        cx={center}
        cy={center}
        r={radius}
        fill="none"
        stroke="currentColor"
        strokeWidth={strokeWidth}
        className="text-gray-200"
      />

      {/* Progress arc */}
      <circle
        cx={center}
        cy={center}
        r={radius}
        fill="none"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
        strokeDasharray={circumference}
        strokeDashoffset={offset}
        style={{
          transition: 'stroke-dashoffset 1s ease-in-out',
        }}
      />

      {/* Milestone markers */}
      {MILESTONES.map((milestone) => {
        const angle = (milestone / 100) * 360 - 90;
        const rad = (angle * Math.PI) / 180;
        const markerX = center + radius * Math.cos(rad);
        const markerY = center + radius * Math.sin(rad);
        const isMet = clampedPercentage >= milestone;

        return (
          <circle
            key={milestone}
            cx={markerX}
            cy={markerY}
            r={3}
            fill={isMet ? color : '#d1d5db'}
            className="transform rotate-90 origin-center"
          />
        );
      })}

      {/* Center text */}
      <text
        x={center}
        y={center}
        textAnchor="middle"
        dominantBaseline="central"
        className="transform rotate-90 origin-center fill-gray-900 font-bold"
        style={{ fontSize: size * 0.22 }}
      >
        {Math.round(clampedPercentage)}%
      </text>
    </svg>
  );
}
