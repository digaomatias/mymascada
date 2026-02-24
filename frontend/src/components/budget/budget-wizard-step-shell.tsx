'use client';

import { cn } from '@/lib/utils';
import { CheckIcon, ArrowLeftIcon, ArrowRightIcon } from '@heroicons/react/24/outline';
import { Button } from '@/components/ui/button';

interface BudgetWizardStepShellProps {
  title: string;
  subtitle: string;
  step: 1 | 2 | 3;
  children: React.ReactNode;
  nextLabel: string;
  backLabel: string;
  onNext?: () => void;
  onBack?: () => void;
  nextDisabled?: boolean;
  nextLoading?: boolean;
  showBack?: boolean;
  hideNext?: boolean;
  className?: string;
}

export function BudgetWizardStepShell({
  title,
  subtitle,
  step,
  children,
  nextLabel,
  backLabel,
  onNext,
  onBack,
  nextDisabled = false,
  nextLoading = false,
  showBack = true,
  hideNext = false,
  className,
}: BudgetWizardStepShellProps) {
  return (
    <div className={cn('space-y-5', className)}>
      <div>
        <h2 className="text-[1.65rem] font-semibold tracking-[-0.03em] text-slate-900">{title}</h2>
        <p className="mt-1 text-sm text-slate-500">{subtitle}</p>
      </div>

      <div className="flex items-center">
        {[1, 2, 3].map((index) => (
          <div key={index} className="flex items-center">
            <div
              className={cn(
                'grid h-8 w-8 place-items-center rounded-full border text-xs font-semibold transition-colors',
                index < step
                  ? 'border-violet-600 bg-violet-600 text-white'
                  : index === step
                    ? 'border-violet-600 bg-violet-600 text-white'
                    : 'border-slate-300 bg-white text-slate-500',
              )}
            >
              {index < step ? <CheckIcon className="h-4 w-4" /> : index}
            </div>
            {index < 3 && (
              <div
                className={cn(
                  'mx-1.5 h-1 w-8 rounded-full sm:w-16',
                  index < step ? 'bg-violet-600' : 'bg-slate-300',
                )}
              />
            )}
          </div>
        ))}
      </div>

      <div className="rounded-[28px] border border-violet-100/80 bg-white/92 p-6 shadow-[0_24px_46px_-34px_rgba(76,29,149,0.42)] sm:p-7">
        {children}

        <div className="mt-6 flex items-center justify-between">
          {showBack ? (
            <Button variant="outline" onClick={onBack}>
              <ArrowLeftIcon className="mr-1.5 h-4 w-4" />
              {backLabel}
            </Button>
          ) : (
            <span />
          )}
          {!hideNext && (
            <Button onClick={onNext} disabled={nextDisabled} loading={nextLoading}>
              {nextLabel}
              <ArrowRightIcon className="ml-1.5 h-4 w-4" />
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
