'use client';

import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { ScaleIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

interface ReconcileAccountButtonProps {
  accountId: number;
  className?: string;
  children?: React.ReactNode;
}

export function ReconcileAccountButton({ 
  accountId, 
  className = '',
  children 
}: ReconcileAccountButtonProps) {
  const router = useRouter();
  const t = useTranslations('accounts');

  const handleClick = () => {
    router.push(`/accounts/${accountId}/reconcile`);
  };

  return (
    <Button 
      onClick={handleClick}
      variant="secondary"
      className={`flex items-center gap-2 ${className}`}
    >
      <ScaleIcon className="w-4 h-4" />
      {children || t('reconcile')}
    </Button>
  );
}
