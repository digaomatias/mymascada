import type { GoalSummary } from '@/lib/api-client';
import {
  ShieldCheckIcon,
  CreditCardIcon,
  ChartBarSquareIcon,
  BanknotesIcon,
  FlagIcon,
} from '@heroicons/react/24/outline';
import { formatCurrency } from '@/lib/utils';

// --- Journey stages (presentation order, not user gating) ---

export type JourneyStage = 'foundation' | 'freedom' | 'growth' | 'dreams';

export const JOURNEY_STAGES: { key: JourneyStage; label: string }[] = [
  { key: 'foundation', label: 'Foundation' },
  { key: 'freedom', label: 'Freedom' },
  { key: 'growth', label: 'Growth' },
  { key: 'dreams', label: 'Dreams' },
];

// --- Goal context for type-specific calculations ---

export interface GoalContext {
  monthlyExpenses: number;
  monthlyIncome: number;
}

// --- Hero metric returned by each config ---

export interface HeroMetric {
  label: string;
  value: string;
  subtext?: string;
}

// --- Accent color set per goal type ---

export interface GoalAccentColor {
  bg: string;
  text: string;
  border: string;
  bar: string;
}

// --- Per-type configuration record ---

export interface GoalTypeConfig {
  icon: typeof ShieldCheckIcon;
  accentColor: GoalAccentColor;
  heroMetric: (goal: GoalSummary, ctx: GoalContext) => HeroMetric | null;
  journeyStage: JourneyStage;
  journeyPriority: number;
}

// --- Tracking state ---

export type GoalTrackingState =
  | 'completed'
  | 'paused'
  | 'overdue'
  | 'behind'
  | 'onTrack'
  | 'noDeadline';

export function getGoalTrackingState(goal: GoalSummary): GoalTrackingState {
  if (goal.status === 'Completed') return 'completed';
  if (goal.status === 'Paused' || goal.status === 'Abandoned') return 'paused';

  if (goal.deadline && goal.daysRemaining !== undefined) {
    if (goal.daysRemaining <= 0) return 'overdue';

    const remainingPct = 100 - goal.progressPercentage;
    if (
      (goal.daysRemaining < 60 && remainingPct / goal.daysRemaining > 2.0) ||
      (goal.daysRemaining <= 14 && goal.progressPercentage < 80)
    ) {
      return 'behind';
    }

    return 'onTrack';
  }

  return 'noDeadline';
}

export const TRACKING_STATE_STYLES: Record<
  GoalTrackingState,
  { bg: string; text: string; border: string; label: string }
> = {
  completed: {
    bg: 'bg-emerald-50',
    text: 'text-emerald-700',
    border: 'border-emerald-200',
    label: 'Complete',
  },
  paused: {
    bg: 'bg-slate-100',
    text: 'text-slate-600',
    border: 'border-slate-200',
    label: 'Paused',
  },
  overdue: {
    bg: 'bg-rose-50',
    text: 'text-rose-700',
    border: 'border-rose-200',
    label: 'Overdue',
  },
  behind: {
    bg: 'bg-amber-50',
    text: 'text-amber-700',
    border: 'border-amber-200',
    label: 'Behind',
  },
  onTrack: {
    bg: 'bg-emerald-50',
    text: 'text-emerald-700',
    border: 'border-emerald-200',
    label: 'On Track',
  },
  noDeadline: {
    bg: 'bg-slate-50',
    text: 'text-slate-500',
    border: 'border-slate-200',
    label: 'No Deadline',
  },
};

// --- Type configs ---

export const GOAL_TYPE_CONFIGS: Record<string, GoalTypeConfig> = {
  EmergencyFund: {
    icon: ShieldCheckIcon,
    accentColor: {
      bg: 'bg-teal-50',
      text: 'text-teal-700',
      border: 'border-teal-200',
      bar: 'bg-teal-500',
    },
    journeyStage: 'foundation',
    journeyPriority: 1,
    heroMetric: (goal, ctx) => {
      if (ctx.monthlyExpenses > 0) {
        const months = goal.currentAmount / ctx.monthlyExpenses;
        return {
          label: `${months.toFixed(1)} months covered`,
          value: `${months.toFixed(1)}`,
          subtext: 'of 3-month target',
        };
      }
      return null;
    },
  },

  DebtPayoff: {
    icon: CreditCardIcon,
    accentColor: {
      bg: 'bg-rose-50',
      text: 'text-rose-700',
      border: 'border-rose-200',
      bar: 'bg-rose-500',
    },
    journeyStage: 'freedom',
    journeyPriority: 2,
    heroMetric: (goal) => {
      const remaining = goal.targetAmount - goal.currentAmount;
      const deadlineStr =
        goal.deadline
          ? new Date(goal.deadline).toLocaleDateString(undefined, {
              month: 'short',
              year: 'numeric',
            })
          : null;
      return {
        label: `Remaining: ${formatCurrency(remaining)}`,
        value: formatCurrency(remaining),
        subtext: deadlineStr ? `Debt-free by ${deadlineStr}` : undefined,
      };
    },
  },

  Investment: {
    icon: ChartBarSquareIcon,
    accentColor: {
      bg: 'bg-blue-50',
      text: 'text-blue-700',
      border: 'border-blue-200',
      bar: 'bg-blue-500',
    },
    journeyStage: 'growth',
    journeyPriority: 3,
    heroMetric: (goal) => {
      const deadlineStr =
        goal.deadline
          ? new Date(goal.deadline).toLocaleDateString(undefined, {
              month: 'short',
              year: 'numeric',
            })
          : null;
      return {
        label: `${goal.progressPercentage.toFixed(0)}% funded`,
        value: `${goal.progressPercentage.toFixed(0)}%`,
        subtext: deadlineStr ? `Target: ${deadlineStr}` : undefined,
      };
    },
  },

  Savings: {
    icon: BanknotesIcon,
    accentColor: {
      bg: 'bg-violet-50',
      text: 'text-violet-700',
      border: 'border-violet-200',
      bar: 'bg-violet-500',
    },
    journeyStage: 'growth',
    journeyPriority: 3,
    heroMetric: (goal) => {
      if (goal.deadline && goal.daysRemaining !== undefined && goal.daysRemaining > 0) {
        return {
          label: `${goal.progressPercentage.toFixed(0)}% saved`,
          value: `${goal.progressPercentage.toFixed(0)}%`,
          subtext: `${goal.daysRemaining} days left`,
        };
      }
      return {
        label: `${goal.progressPercentage.toFixed(0)}% saved`,
        value: `${goal.progressPercentage.toFixed(0)}%`,
      };
    },
  },

  Custom: {
    icon: FlagIcon,
    accentColor: {
      bg: 'bg-slate-50',
      text: 'text-slate-700',
      border: 'border-slate-200',
      bar: 'bg-slate-500',
    },
    journeyStage: 'dreams',
    journeyPriority: 4,
    heroMetric: (goal) => {
      if (goal.deadline && goal.daysRemaining !== undefined && goal.daysRemaining > 0) {
        return {
          label: `${goal.progressPercentage.toFixed(0)}% complete`,
          value: `${goal.progressPercentage.toFixed(0)}%`,
          subtext: `${goal.daysRemaining} days left`,
        };
      }
      return null;
    },
  },
};

// Fallback for unknown types
const DEFAULT_CONFIG: GoalTypeConfig = GOAL_TYPE_CONFIGS.Custom;

export function getGoalTypeConfig(goalType: string): GoalTypeConfig {
  return GOAL_TYPE_CONFIGS[goalType] ?? DEFAULT_CONFIG;
}

// --- Sorting ---

export function sortGoalsByJourney(goals: GoalSummary[]): GoalSummary[] {
  return [...goals].sort((a, b) => {
    const aConfig = getGoalTypeConfig(a.goalType);
    const bConfig = getGoalTypeConfig(b.goalType);
    const aState = getGoalTrackingState(a);
    const bState = getGoalTrackingState(b);

    // Completed/paused always last
    const aInactive = aState === 'completed' || aState === 'paused';
    const bInactive = bState === 'completed' || bState === 'paused';
    if (aInactive !== bInactive) return aInactive ? 1 : -1;

    // Pinned goals first (among active goals)
    if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1;

    // Journey priority
    if (aConfig.journeyPriority !== bConfig.journeyPriority) {
      return aConfig.journeyPriority - bConfig.journeyPriority;
    }

    // Within same priority: overdue first, then behind, then by days remaining
    const stateOrder: Record<GoalTrackingState, number> = {
      overdue: 0,
      behind: 1,
      onTrack: 2,
      noDeadline: 3,
      completed: 4,
      paused: 5,
    };
    if (stateOrder[aState] !== stateOrder[bState]) {
      return stateOrder[aState] - stateOrder[bState];
    }

    // Nearest deadline first
    return (a.daysRemaining ?? 9999) - (b.daysRemaining ?? 9999);
  });
}

// --- Journey nudge logic ---

export interface GoalNudge {
  tone: 'amber' | 'rose' | 'emerald';
  message: string;
  ctaLabel?: string;
  ctaHref?: string;
}

export function pickGoalNudge(
  goals: GoalSummary[],
  ctx: GoalContext,
): GoalNudge {
  const active = goals.filter(
    (g) => g.status !== 'Completed' && g.status !== 'Paused' && g.status !== 'Abandoned',
  );

  // No emergency fund goal at all
  const hasEmergencyFund = goals.some((g) => g.goalType === 'EmergencyFund');
  if (!hasEmergencyFund) {
    return {
      tone: 'amber',
      message:
        'Financial experts recommend building an emergency fund first. It protects your other goals from unexpected expenses.',
      ctaLabel: 'Create emergency fund',
      ctaHref: '/goals/new',
    };
  }

  // Emergency fund < 50%
  const emergencyFund = active.find((g) => g.goalType === 'EmergencyFund');
  if (emergencyFund && emergencyFund.progressPercentage < 50) {
    const months =
      ctx.monthlyExpenses > 0
        ? (emergencyFund.currentAmount / ctx.monthlyExpenses).toFixed(1)
        : emergencyFund.progressPercentage.toFixed(0) + '%';
    return {
      tone: 'amber',
      message: `Your safety net covers ${months} months. Financial experts recommend building to 3 months before focusing on other goals.`,
      ctaLabel: 'View emergency fund',
      ctaHref: `/goals/${emergencyFund.id}`,
    };
  }

  // Overdue goal
  const overdue = active.find(
    (g) => g.deadline && g.daysRemaining !== undefined && g.daysRemaining <= 0,
  );
  if (overdue) {
    return {
      tone: 'rose',
      message: `'${overdue.name}' is past its deadline. Review your target or timeline.`,
      ctaLabel: 'Review goal',
      ctaHref: `/goals/${overdue.id}`,
    };
  }

  // Near completion (>= 90%)
  const nearComplete = active.find((g) => g.progressPercentage >= 90);
  if (nearComplete) {
    return {
      tone: 'emerald',
      message: `Almost there! '${nearComplete.name}' is ${nearComplete.progressPercentage.toFixed(0)}% funded.`,
      ctaLabel: 'View goal',
      ctaHref: `/goals/${nearComplete.id}`,
    };
  }

  // All on track
  return {
    tone: 'emerald',
    message: 'Your goals are on track. Keep the momentum going!',
  };
}
