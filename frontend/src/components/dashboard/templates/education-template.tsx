'use client';

import { CSSProperties } from 'react';
import { FinancialRunwayCard } from '@/components/dashboard/cards/financial-runway-card';
import { GoalSpotlightCard } from '@/components/dashboard/cards/goal-spotlight-card';
import { CashflowChartCard } from '@/components/dashboard/cards/cashflow-chart-card';
import { AttentionItemsCard } from '@/components/dashboard/cards/attention-items-card';
import { RecentTransactionsCard } from '@/components/dashboard/cards/recent-transactions-card';
import { BudgetHealthCard } from '@/components/dashboard/cards/budget-health-card';
import { WalletSummaryCard } from '@/components/dashboard/cards/wallet-summary-card';
import { GettingStartedSection } from '@/components/dashboard/getting-started-section';

function stagger(index: number): CSSProperties {
  return { '--stagger': index } as CSSProperties;
}

export function EducationTemplate() {
  return (
    <div className="space-y-5">
      {/* Getting Started — visible only for new users with no accounts */}
      <GettingStartedSection />

      {/* Row 1: Financial Runway + Goal Spotlight */}
      <section
        className="grid gap-5 lg:grid-cols-[minmax(0,1.35fr)_minmax(0,0.65fr)] animate-card-entrance"
        style={stagger(0)}
      >
        <FinancialRunwayCard />
        <GoalSpotlightCard />
      </section>

      {/* Row 2: Cashflow Chart */}
      <section className="animate-card-entrance" style={stagger(1)}>
        <CashflowChartCard />
      </section>

      {/* Row 3: Attention Items + Recent Transactions */}
      <section
        className="grid gap-5 lg:grid-cols-2 animate-card-entrance"
        style={stagger(2)}
      >
        <AttentionItemsCard />
        <RecentTransactionsCard />
      </section>

      {/* Row 4: Budget Health */}
      <section className="animate-card-entrance" style={stagger(3)}>
        <BudgetHealthCard />
      </section>

      {/* Row 5: Wallet Pots */}
      <section className="animate-card-entrance" style={stagger(4)}>
        <WalletSummaryCard />
      </section>
    </div>
  );
}
