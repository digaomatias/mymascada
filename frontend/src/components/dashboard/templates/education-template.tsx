'use client';

import { FinancialRunwayCard } from '@/components/dashboard/cards/financial-runway-card';
import { GoalSpotlightCard } from '@/components/dashboard/cards/goal-spotlight-card';
import { CashflowChartCard } from '@/components/dashboard/cards/cashflow-chart-card';
import { AttentionItemsCard } from '@/components/dashboard/cards/attention-items-card';
import { RecentTransactionsCard } from '@/components/dashboard/cards/recent-transactions-card';
import { BudgetHealthCard } from '@/components/dashboard/cards/budget-health-card';
import { GettingStartedSection } from '@/components/dashboard/getting-started-section';

export function EducationTemplate() {
  return (
    <div className="space-y-5">
      {/* Getting Started — visible only for new users with no accounts */}
      <GettingStartedSection />

      {/* Row 1: Financial Runway + Goal Spotlight */}
      <section className="grid gap-5 lg:grid-cols-[minmax(0,1.35fr)_minmax(0,0.65fr)]">
        <FinancialRunwayCard />
        <GoalSpotlightCard />
      </section>

      {/* Row 2: Cashflow Chart */}
      <section>
        <CashflowChartCard />
      </section>

      {/* Row 3: Attention Items + Recent Transactions */}
      <section className="grid gap-5 lg:grid-cols-2">
        <AttentionItemsCard />
        <RecentTransactionsCard />
      </section>

      {/* Row 4: Budget Health */}
      <section>
        <BudgetHealthCard />
      </section>
    </div>
  );
}
