'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { apiClient } from '@/lib/api-client';
import { UpcomingBillsResponse, UpcomingBillDto } from '@/types/upcoming-bills';
import { formatCurrency } from '@/lib/utils';
import {
  CalendarDaysIcon,
} from '@heroicons/react/24/outline';

export function UpcomingBillsWidget() {
  const t = useTranslations('upcomingBills');
  const [data, setData] = useState<UpcomingBillsResponse | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadUpcomingBills = async () => {
      try {
        setLoading(true);
        const response = await apiClient.getUpcomingBills(7);
        setData(response);
      } catch (error) {
        console.error('Failed to load upcoming bills:', error);
        setData({ bills: [], totalBillsCount: 0, totalExpectedAmount: 0 });
      } finally {
        setLoading(false);
      }
    };

    loadUpcomingBills();
  }, []);

  const formatDueDate = (daysUntilDue: number): string => {
    if (daysUntilDue === 0) return t('today');
    if (daysUntilDue === 1) return t('tomorrow');
    return t('inDays', { days: daysUntilDue });
  };

  if (loading) {
    return (
      <Card className="bg-white/90 backdrop-blur-xs border-0 border-l-4 border-l-info-500 shadow-lg">
        <CardHeader className="pb-2">
          <div className="flex items-center gap-2">
            <Skeleton className="h-6 w-6" />
            <Skeleton className="h-6 w-32" />
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </CardContent>
      </Card>
    );
  }

  // Hide entirely when there are no upcoming bills
  if (!data || data.bills.length === 0) {
    return null;
  }

  return (
    <Card className="bg-white/90 backdrop-blur-xs border-0 border-l-4 border-l-info-500 shadow-lg">
      <CardHeader className="pb-2">
        <div className="flex items-center gap-2">
          <CalendarDaysIcon className="h-6 w-6 text-info-600" />
          <CardTitle className="text-xl font-bold text-gray-900">{t('title')}</CardTitle>
        </div>
        <p className="text-sm text-gray-500">{t('next7Days')}</p>
      </CardHeader>
      <CardContent className="space-y-3">
        {/* Bill list (up to 3) */}
        {data.bills.slice(0, 3).map((bill, index) => (
          <BillItem key={index} bill={bill} formatDueDate={formatDueDate} t={t} />
        ))}

        {/* Footer with total */}
        <div className="pt-2 border-t border-gray-100">
          <div className="flex justify-between items-center text-sm">
            <span className="text-gray-600">{t('totalExpected')}</span>
            <span className="font-semibold text-red-600">
              {formatCurrency(data.totalExpectedAmount)}
            </span>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

interface BillItemProps {
  bill: UpcomingBillDto;
  formatDueDate: (days: number) => string;
  t: ReturnType<typeof useTranslations>;
}

function BillItem({ bill, formatDueDate, t }: BillItemProps) {
  return (
    <div className="flex items-center justify-between p-3 rounded-lg border border-gray-100 hover:bg-gray-50 transition-colors">
      <div className="flex-1 min-w-0">
        <h4 className="font-medium text-gray-900 truncate">{bill.merchantName}</h4>
        <div className="flex items-center gap-2 mt-1">
          <span className="text-xs text-gray-500">{formatDueDate(bill.daysUntilDue)}</span>
          <Badge
            variant="outline"
            className={
              bill.confidenceLevel === 'High'
                ? 'border-green-500 text-green-600 bg-green-50 text-xs'
                : 'border-yellow-500 text-yellow-600 bg-yellow-50 text-xs'
            }
          >
            {bill.confidenceLevel === 'High' ? t('highConfidence') : t('mediumConfidence')}
          </Badge>
        </div>
      </div>
      <div className="text-right flex-shrink-0 ml-3">
        <p className="font-semibold text-red-600">{formatCurrency(bill.expectedAmount)}</p>
        <p className="text-xs text-gray-500">{bill.interval}</p>
      </div>
    </div>
  );
}
