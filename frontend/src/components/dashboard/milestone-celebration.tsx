'use client';

import { useEffect } from 'react';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

interface MilestoneCelebrationProps {
  goalId: number;
  percentage: number;
  goalName: string;
}

const MILESTONES = [25, 50, 75, 100] as const;
type Milestone = (typeof MILESTONES)[number];

const MILESTONE_KEYS: Record<Milestone, string> = {
  25: 'quarter',
  50: 'half',
  75: 'threeQuarters',
  100: 'complete',
};

function getStorageKey(goalId: number): string {
  return `mymascada_milestones_${goalId}`;
}

function getSeenMilestones(goalId: number): number[] {
  try {
    const stored = localStorage.getItem(getStorageKey(goalId));
    return stored ? JSON.parse(stored) : [];
  } catch {
    return [];
  }
}

function markMilestoneSeen(goalId: number, milestone: number): void {
  try {
    const seen = getSeenMilestones(goalId);
    if (!seen.includes(milestone)) {
      seen.push(milestone);
      localStorage.setItem(getStorageKey(goalId), JSON.stringify(seen));
    }
  } catch {
    // Ignore localStorage errors
  }
}

export function MilestoneCelebration({
  goalId,
  percentage,
  goalName,
}: MilestoneCelebrationProps) {
  const t = useTranslations('dashboard.milestones');

  useEffect(() => {
    const seen = getSeenMilestones(goalId);

    for (const milestone of MILESTONES) {
      if (percentage >= milestone && !seen.includes(milestone)) {
        const message = t(MILESTONE_KEYS[milestone], { goalName });
        if (milestone === 100) {
          toast.success(message);
        } else {
          toast(message);
        }
        markMilestoneSeen(goalId, milestone);
      }
    }
  }, [goalId, percentage, goalName, t]);

  return null;
}
