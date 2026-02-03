// Budget Types

export interface BudgetSummary {
  id: number;
  name: string;
  description?: string;
  periodType: string;
  startDate: string;
  endDate: string;
  isRecurring: boolean;
  isActive: boolean;
  categoryCount: number;
  totalBudgeted: number;
  totalSpent: number;
  totalRemaining: number;
  usedPercentage: number;
  daysRemaining: number;
  isCurrentPeriod: boolean;
}

export interface BudgetDetail {
  id: number;
  name: string;
  description?: string;
  periodType: string;
  startDate: string;
  endDate: string;
  isRecurring: boolean;
  isActive: boolean;
  totalBudgeted: number;
  totalSpent: number;
  totalRemaining: number;
  usedPercentage: number;
  daysRemaining: number;
  totalDays: number;
  periodElapsedPercentage: number;
  categories: BudgetCategoryProgress[];
}

export interface BudgetCategoryProgress {
  categoryId: number;
  categoryName: string;
  categoryColor?: string;
  categoryIcon?: string;
  parentCategoryName?: string;
  budgetedAmount: number;
  rolloverAmount: number;
  effectiveBudget: number;
  actualSpent: number;
  remainingAmount: number;
  usedPercentage: number;
  isOverBudget: boolean;
  isApproachingLimit: boolean;
  transactionCount: number;
  allowRollover: boolean;
  includeSubcategories: boolean;
  status: 'OnTrack' | 'Approaching' | 'Over';
}

export interface BudgetSuggestion {
  categoryId: number;
  categoryName: string;
  categoryColor?: string;
  categoryIcon?: string;
  parentCategoryName?: string;
  averageMonthlySpending: number;
  suggestedBudget: number;
  minSpending: number;
  maxSpending: number;
  monthsAnalyzed: number;
  totalTransactionCount: number;
  confidence: number;
  // Enhanced fields
  spendingTrend: 'Increasing' | 'Decreasing' | 'Stable';
  trendPercentage: number;
  projectedNextMonth: number;
  priorityScore: number;
  recommendationType: 'Essential' | 'Regular' | 'Discretionary' | 'SavingsOpportunity';
  insight?: string;
  percentageOfTotal: number;
  lastMonthSpending: number;
}

export type SpendingTrend = 'Increasing' | 'Decreasing' | 'Stable';
export type RecommendationType = 'Essential' | 'Regular' | 'Discretionary' | 'SavingsOpportunity';

export function getTrendColor(trend: SpendingTrend): string {
  switch (trend) {
    case 'Increasing':
      return 'text-red-600';
    case 'Decreasing':
      return 'text-green-600';
    case 'Stable':
      return 'text-gray-600';
    default:
      return 'text-gray-600';
  }
}

export function getRecommendationBadgeColor(type: RecommendationType): string {
  switch (type) {
    case 'Essential':
      return 'bg-blue-100 text-blue-800';
    case 'Regular':
      return 'bg-gray-100 text-gray-800';
    case 'Discretionary':
      return 'bg-yellow-100 text-yellow-800';
    case 'SavingsOpportunity':
      return 'bg-green-100 text-green-800';
    default:
      return 'bg-gray-100 text-gray-800';
  }
}

export interface CreateBudgetRequest {
  name: string;
  description?: string;
  periodType: string;
  startDate: string;
  endDate?: string;
  isRecurring: boolean;
  categories: CreateBudgetCategoryRequest[];
}

export interface CreateBudgetCategoryRequest {
  categoryId: number;
  budgetedAmount: number;
  allowRollover?: boolean;
  carryOverspend?: boolean;
  includeSubcategories?: boolean;
  notes?: string;
}

export interface UpdateBudgetRequest {
  name?: string;
  description?: string;
  isActive?: boolean;
  isRecurring?: boolean;
}

export interface UpdateBudgetCategoryRequest {
  budgetedAmount?: number;
  allowRollover?: boolean;
  carryOverspend?: boolean;
  includeSubcategories?: boolean;
  notes?: string;
}

export type BudgetPeriodType = 'Monthly' | 'Weekly' | 'Biweekly' | 'Custom';

export type BudgetStatus = 'OnTrack' | 'Approaching' | 'Over';

export function getBudgetStatusColor(status: BudgetStatus): string {
  switch (status) {
    case 'OnTrack':
      return 'text-green-600 bg-green-100';
    case 'Approaching':
      return 'text-yellow-600 bg-yellow-100';
    case 'Over':
      return 'text-red-600 bg-red-100';
    default:
      return 'text-gray-600 bg-gray-100';
  }
}

export function getProgressBarColor(usedPercentage: number): string {
  if (usedPercentage >= 100) return 'bg-red-500';
  if (usedPercentage >= 80) return 'bg-yellow-500';
  return 'bg-green-500';
}

export function formatCurrency(amount: number, currency = 'NZD'): string {
  return new Intl.NumberFormat('en-NZ', {
    style: 'currency',
    currency,
  }).format(amount);
}

// Rollover Types

export interface BudgetRolloverResult {
  processedAt: string;
  previewOnly: boolean;
  totalBudgetsProcessed: number;
  newBudgetsCreated: number;
  totalRolloverAmount: number;
  message: string;
  processedBudgets: BudgetRollover[];
}

export interface BudgetRollover {
  sourceBudgetId: number;
  sourceBudgetName: string;
  periodStartDate: string;
  periodEndDate: string;
  isRecurring: boolean;
  totalRollover: number;
  newBudgetCreated: boolean;
  newBudgetId?: number;
  newPeriodStartDate?: string;
  newPeriodEndDate?: string;
  categoryRollovers: CategoryRollover[];
}

export interface CategoryRollover {
  categoryId: number;
  categoryName: string;
  budgetedAmount: number;
  actualSpent: number;
  remainingAmount: number;
  rolloverAmount: number;
  carryOverspend: boolean;
  status: 'Surplus' | 'Deficit';
}
