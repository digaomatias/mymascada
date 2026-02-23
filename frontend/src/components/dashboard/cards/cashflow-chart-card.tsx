'use client';

import { useEffect, useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { DashboardCard } from '@/components/dashboard/dashboard-card';
import { apiClient } from '@/lib/api-client';

interface MonthData {
  label: string;
  income: number;
  expenses: number;
}

function CashflowChart({ data }: { data: MonthData[] }) {
  const income = data.map((d) => d.income);
  const expenses = data.map((d) => d.expenses);
  const months = data.map((d) => d.label);

  const w = 600;
  const h = 170;
  const padX = 24;
  const padY = 14;
  const plotW = w - padX * 2;
  const plotH = h - padY * 2;
  const allVals = [...income, ...expenses].filter((v) => v > 0);
  const max = allVals.length > 0 ? Math.max(...allVals) : 1;
  const min = allVals.length > 0 ? Math.min(...allVals) * 0.85 : 0;
  const range = Math.max(max - min, 1);

  const toPoints = useMemo(
    () => (vals: number[]) =>
      vals.map((v, i) => {
        const x = padX + (i / Math.max(vals.length - 1, 1)) * plotW;
        const y = padY + plotH - ((v - min) / range) * plotH;
        return [x, y] as const;
      }),
    [padX, padY, plotW, plotH, min, range],
  );

  const incPts = toPoints(income);
  const expPts = toPoints(expenses);

  const polyline = (pts: readonly (readonly [number, number])[]) =>
    pts.map(([x, y]) => `${x},${y}`).join(' ');

  const area = (pts: readonly (readonly [number, number])[]) =>
    `${pts.map(([x, y]) => `${x},${y}`).join(' ')} ${padX + plotW},${padY + plotH} ${padX},${padY + plotH}`;

  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="h-40 w-full">
      <defs>
        <linearGradient id="cf-inc-fill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#10b981" stopOpacity="0.18" />
          <stop offset="100%" stopColor="#10b981" stopOpacity="0.01" />
        </linearGradient>
        <linearGradient id="cf-exp-fill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#8b5cf6" stopOpacity="0.10" />
          <stop offset="100%" stopColor="#8b5cf6" stopOpacity="0.01" />
        </linearGradient>
      </defs>
      {/* Grid lines */}
      {[0, 0.5, 1].map((f) => {
        const y = padY + plotH * (1 - f);
        const val = min + range * f;
        return (
          <g key={f}>
            <line x1={padX} y1={y} x2={padX + plotW} y2={y} stroke="#e8e4f0" strokeWidth="0.8" strokeDasharray="4 4" />
            <text x={padX - 8} y={y + 4} textAnchor="end" fontSize="10" fill="#8b8b9e" fontFamily="var(--font-dash-mono), monospace">
              {(val / 1000).toFixed(0)}k
            </text>
          </g>
        );
      })}
      {/* Month labels */}
      {months.map((m, i) => {
        const x = padX + (i / Math.max(months.length - 1, 1)) * plotW;
        return (
          <text key={`${m}-${i}`} x={x} y={h - 4} textAnchor="middle" fontSize="10" fill="#8b8b9e" fontFamily="var(--font-dash-sans), system-ui">
            {m}
          </text>
        );
      })}
      {/* Areas and lines */}
      {incPts.length > 1 && (
        <>
          <polygon points={area(incPts)} fill="url(#cf-inc-fill)" />
          <polyline points={polyline(incPts)} fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
          <circle cx={incPts[incPts.length - 1][0]} cy={incPts[incPts.length - 1][1]} r="5" fill="#fff" stroke="#10b981" strokeWidth="2.5" />
        </>
      )}
      {expPts.length > 1 && (
        <>
          <polygon points={area(expPts)} fill="url(#cf-exp-fill)" />
          <polyline points={polyline(expPts)} fill="none" stroke="#8b5cf6" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" opacity="0.7" />
          <circle cx={expPts[expPts.length - 1][0]} cy={expPts[expPts.length - 1][1]} r="5" fill="#fff" stroke="#8b5cf6" strokeWidth="2.5" />
        </>
      )}
    </svg>
  );
}

const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

export function CashflowChartCard() {
  const t = useTranslations('dashboard.cards.cashflow');
  const [chartData, setChartData] = useState<MonthData[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        setError(null);

        const now = new Date();
        const promises: Promise<{ totalIncome: number; totalExpenses: number; month: number; year: number }>[] = [];

        // Load last 7 months
        for (let i = 6; i >= 0; i--) {
          const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
          const year = d.getFullYear();
          const month = d.getMonth() + 1;
          promises.push(
            apiClient
              .getMonthlySummary(year, month)
              .then((s) => ({
                ...(s as { totalIncome: number; totalExpenses: number }),
                month,
                year,
              }))
              .catch(() => ({ totalIncome: 0, totalExpenses: 0, month, year })),
          );
        }

        const results = await Promise.all(promises);
        const data: MonthData[] = results.map((r) => ({
          label: MONTH_LABELS[r.month - 1],
          income: r.totalIncome || 0,
          expenses: r.totalExpenses || 0,
        }));

        setChartData(data);
      } catch (err) {
        console.error('Failed to load cashflow data:', err);
        setError('Failed to load chart data');
      } finally {
        setLoading(false);
      }
    };

    load();
  }, []);

  return (
    <DashboardCard cardId="cashflow-chart" loading={loading} error={error}>
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold tracking-[-0.02em] text-slate-900">
            {t('title')}
          </h3>
          <p className="mt-1 text-sm text-slate-500">
            {t('subtitle')}
          </p>
        </div>
        <div className="flex items-center gap-5 text-xs font-medium text-slate-500">
          <span className="flex items-center gap-2">
            <span className="inline-block h-[3px] w-4 rounded-full bg-emerald-500" /> {t('income')}
          </span>
          <span className="flex items-center gap-2">
            <span className="inline-block h-[3px] w-4 rounded-full bg-violet-400" /> {t('expenses')}
          </span>
        </div>
      </div>
      <div className="mt-4">
        {chartData.length > 0 ? (
          <CashflowChart data={chartData} />
        ) : (
          <div className="flex h-48 items-center justify-center text-sm text-slate-400">
            {t('noData')}
          </div>
        )}
      </div>
    </DashboardCard>
  );
}
