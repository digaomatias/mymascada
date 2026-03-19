'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { Card, CardContent } from '@/components/ui/card';
import { apiClient } from '@/lib/api-client';
import { LightBulbIcon, SparklesIcon } from '@heroicons/react/24/outline';
import type { CoachingInsightResponse } from '@/types/api-responses';

interface CoachingInsightCardProps {
  monthlyIncome: number;
  monthlyExpenses: number;
}

export function CoachingInsightCard({
  // Props kept for backward compatibility but data now comes from the API
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  monthlyIncome,
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  monthlyExpenses,
}: CoachingInsightCardProps) {
  const t = useTranslations('dashboard.coaching');
  const [insight, setInsight] = useState<CoachingInsightResponse | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadInsight = async () => {
      try {
        const data = await apiClient.getCoachingInsight();
        setInsight(data);
      } catch (error) {
        console.error('Failed to load coaching insight:', error);
      } finally {
        setLoading(false);
      }
    };

    loadInsight();
  }, []);

  if (loading || !insight) {
    return null;
  }

  const IconComponent = insight.insightIcon === 'lightbulb' ? LightBulbIcon : SparklesIcon;

  return (
    <Card className="bg-white/90 backdrop-blur-xs border-0 border-l-4 border-l-primary-500 shadow-lg">
      <CardContent className="p-4 lg:p-6">
        <div className="flex items-start gap-3">
          <div className="flex-shrink-0 w-10 h-10 rounded-full bg-primary-100 flex items-center justify-center">
            <IconComponent className="w-5 h-5 text-primary-600" />
          </div>
          <div>
            <p className="text-xs font-semibold text-primary-600 uppercase tracking-wide mb-1">
              {t('title')}
            </p>
            <p className="text-sm text-ink-700 leading-relaxed">
              {t(insight.insightKey, insight.insightParams ?? {})}
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
