export interface DashboardSummaryResponse {
  totalBalance: number;
  monthlyIncome: number;
  monthlyExpenses: number;
  transactionCount: number;
  recentTransactions: RecentTransactionDto[];
  runwayMonths: number;
  savingsRate: number;
  netSaved: number;
  avgMonthlyIncome: number;
  avgMonthlyExpenses: number;
  totalAssets: number;
  totalLiabilities: number;
  netWorth: number;
  isUsingFallbackMonth: boolean;
  displayMonth: number;
  displayYear: number;
}

export interface RecentTransactionDto {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  userDescription?: string;
  accountName: string;
  categoryName?: string;
  categoryColor?: string;
}

export interface CashflowHistoryResponse {
  months: CashflowMonthDto[];
}

export interface CashflowMonthDto {
  year: number;
  month: number;
  label: string;
  income: number;
  expenses: number;
  net: number;
}

export interface AnalyticsSummaryResponse {
  totalIncome: number;
  totalExpenses: number;
  avgMonthlyIncome: number;
  avgMonthlyExpenses: number;
  netAmount: number;
  savingsRate: number;
  monthCount: number;
  bestMonth?: MonthHighlightDto;
  worstMonth?: MonthHighlightDto;
  monthlyTrends: MonthlyTrendDto[];
  yearlyComparisons: YearlyComparisonDto[];
}

export interface MonthHighlightDto {
  year: number;
  month: number;
  label: string;
  netAmount: number;
}

export interface MonthlyTrendDto {
  year: number;
  month: number;
  label: string;
  income: number;
  expenses: number;
  net: number;
  savingsRate: number;
}

export interface YearlyComparisonDto {
  year: number;
  totalIncome: number;
  totalExpenses: number;
  netAmount: number;
  savingsRate: number;
  monthCount: number;
}

export interface AttentionItemsResponse {
  items: AttentionItemDto[];
}

export interface AttentionItemDto {
  type: string;
  severity: string;
  count?: number;
  entityName?: string;
  amount?: number;
  daysUntilDue?: number;
  annualizedAmount?: number;
}

export interface BudgetHealthSummaryResponse {
  overCount: number;
  atRiskCount: number;
  onTrackCount: number;
  inactiveCount: number;
  totalBudgeted: number;
  totalSpent: number;
  overallPercentage: number;
  budgetsOverLimit: number;
  budgetsApproaching: number;
  nearestDeadlineDays?: number;
  budgets: BudgetRiskItemDto[];
}

export interface BudgetRiskItemDto {
  budgetId: number;
  name: string;
  riskState: string;
  priorityScore: number;
  expectedSpent: number;
  variance: number;
  variancePercentage: number;
  isOverspendingPace: boolean;
}

export interface CoachingInsightResponse {
  insightKey: string;
  insightParams?: Record<string, string>;
  insightIcon: string;
  nudgeTone: string;
  nudgeKey: string;
  nudgeTargetGoalId?: number;
}

// Extended goal summary from enriched API
export interface GoalSummaryEnriched {
  id: number;
  name: string;
  description?: string;
  targetAmount: number;
  currentAmount: number;
  progressPercentage: number;
  remainingAmount: number;
  goalType: string;
  status: string;
  deadline?: string;
  daysRemaining?: number;
  linkedAccountName?: string;
  isPinned: boolean;
  trackingState: string;
  journeyStage: string;
  journeyPriority: number;
  sortOrder: number;
  currentMilestone?: number;
  nextMilestone?: number;
}

// Extended category trend from enriched API
export interface CategoryTrendEnriched {
  categoryId: number;
  categoryName: string;
  categoryColor?: string;
  totalSpent: number;
  averageMonthlySpent: number;
  trend: string;
  trendPercentage: number;
  highestMonth?: PeriodAmountDto;
  lowestMonth?: PeriodAmountDto;
  periods: PeriodAmountDto[];
}

export interface PeriodAmountDto {
  periodStart: string;
  periodLabel: string;
  amount: number;
  transactionCount: number;
}

export interface CategoryTrendsEnrichedResponse {
  startDate: string;
  endDate: string;
  totalSpending: number;
  avgMonthlySpending: number;
  categories: CategoryTrendEnriched[];
  periodSummaries: {
    periodStart: string;
    periodLabel: string;
    totalSpent: number;
    transactionCount: number;
  }[];
}
