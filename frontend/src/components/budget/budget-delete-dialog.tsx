'use client';

import { useState } from 'react';
import type { ReactNode } from 'react';
import { useTranslations } from 'next-intl';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { ExclamationTriangleIcon } from '@heroicons/react/24/outline';
import { cn } from '@/lib/utils';

interface BudgetDeleteDialogProps {
  budgetName: string;
  onConfirm: () => Promise<void> | void;
  trigger: ReactNode;
}

export function BudgetDeleteDialog({
  budgetName,
  onConfirm,
  trigger,
}: BudgetDeleteDialogProps) {
  const t = useTranslations('budgets');
  const tCommon = useTranslations('common');
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const handleConfirm = async () => {
    try {
      setBusy(true);
      await onConfirm();
      setOpen(false);
    } finally {
      setBusy(false);
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={setOpen}>
      <AlertDialogTrigger asChild>{trigger}</AlertDialogTrigger>
      <AlertDialogContent className="max-w-[560px] rounded-[24px] border border-violet-200/80 bg-white/95 p-0 shadow-[0_28px_58px_-28px_rgba(76,29,149,0.45)]">
        <AlertDialogHeader className="border-b border-violet-100/80 p-6 pb-4 text-left">
          <div className="mb-2 inline-flex h-10 w-10 items-center justify-center rounded-xl bg-rose-100 text-rose-600">
            <ExclamationTriangleIcon className="h-5 w-5" />
          </div>
          <AlertDialogTitle className="text-2xl font-semibold tracking-[-0.02em] text-slate-900">
            {t('deleteBudget')}
          </AlertDialogTitle>
          <AlertDialogDescription className="mt-2 text-sm leading-relaxed text-slate-600">
            {t('deleteConfirm', { name: budgetName })}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter className="p-6 pt-4">
          <AlertDialogCancel className="rounded-xl border-violet-200 bg-white text-slate-700 hover:bg-violet-50 hover:text-violet-700">
            {tCommon('cancel')}
          </AlertDialogCancel>
          <AlertDialogAction
            onClick={handleConfirm}
            disabled={busy}
            className={cn(
              'rounded-xl bg-rose-600 text-white hover:bg-rose-700',
              busy && 'cursor-wait opacity-75',
            )}
          >
            {busy ? tCommon('loading') : tCommon('delete')}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
