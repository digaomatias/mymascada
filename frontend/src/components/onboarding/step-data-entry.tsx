'use client';

import { ComponentType } from 'react';
import { useTranslations } from 'next-intl';
import {
  PencilSquareIcon,
  ArrowUpTrayIcon,
  BuildingLibraryIcon,
} from '@heroicons/react/24/outline';
import { CheckCircleIcon } from '@heroicons/react/24/solid';
import { Button } from '@/components/ui/button';

interface StepDataEntryProps {
  value: string;
  onChange: (method: string) => void;
  onNext: () => void;
  onBack: () => void;
  isSubmitting?: boolean;
}

interface DataEntryOption {
  key: string;
  icon: ComponentType<{ className?: string }>;
  disabled: boolean;
  recommended: boolean;
}

const DATA_ENTRY_OPTIONS: DataEntryOption[] = [
  { key: 'manual', icon: PencilSquareIcon, disabled: false, recommended: true },
  { key: 'csv', icon: ArrowUpTrayIcon, disabled: false, recommended: false },
  { key: 'bank', icon: BuildingLibraryIcon, disabled: true, recommended: false },
];

export function StepDataEntry({ value, onChange, onNext, onBack, isSubmitting = false }: StepDataEntryProps) {
  const t = useTranslations('onboarding');

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h2 className="font-[var(--font-onboarding-sans)] text-3xl font-semibold tracking-[-0.02em] text-slate-900">
          {t('dataEntry.title')}
        </h2>
        <p className="max-w-xl text-sm leading-relaxed text-slate-600 sm:text-base">
          {t('dataEntry.subtitle')}
        </p>
      </div>

      <div className="grid gap-3 lg:grid-cols-3">
        {DATA_ENTRY_OPTIONS.map((option) => {
          const Icon = option.icon;
          const isSelected = value === option.key;

          return (
            <button
              type="button"
              key={option.key}
              className={`group relative min-h-[168px] overflow-hidden rounded-2xl border p-4 text-left transition-all duration-200 ${
                option.disabled
                  ? 'cursor-not-allowed border-slate-200 bg-slate-50/90'
                  : isSelected
                    ? 'border-violet-500 bg-violet-50 shadow-[0_20px_35px_-28px_rgba(124,58,237,0.95)] ring-4 ring-violet-200/60'
                    : 'border-slate-200 bg-white hover:-translate-y-0.5 hover:border-violet-300 hover:shadow-[0_20px_35px_-28px_rgba(124,58,237,0.7)]'
              }`}
              onClick={() => {
                if (!option.disabled) {
                  onChange(option.key);
                }
              }}
              disabled={option.disabled}
            >
              {option.disabled && (
                <div
                  className="pointer-events-none absolute inset-0 rounded-2xl opacity-30"
                  style={{
                    backgroundImage: 'repeating-linear-gradient(-45deg, rgba(148,163,184,0.3) 0px, rgba(148,163,184,0.3) 8px, rgba(255,255,255,0) 8px, rgba(255,255,255,0) 16px)',
                  }}
                />
              )}

              {option.recommended && !option.disabled && (
                <span className="absolute right-3 top-3 rounded-full bg-violet-600 px-2 py-0.5 text-xs font-semibold text-white">
                  {t('dataEntry.recommended')}
                </span>
              )}

              {option.disabled && (
                <span className="absolute right-3 top-3 rounded-full bg-slate-200 px-2 py-0.5 text-xs font-medium text-slate-600">
                  {t('dataEntry.bankDisabled')}
                </span>
              )}

              <div className="relative flex h-full flex-col">
                <div className={`grid h-11 w-11 place-items-center rounded-xl ${
                  option.disabled
                    ? 'bg-slate-100 ring-1 ring-slate-200'
                    : isSelected
                      ? 'bg-violet-100 ring-1 ring-violet-200'
                      : 'bg-violet-50 ring-1 ring-violet-100'
                }`}>
                  <Icon className={`w-6 h-6 ${
                    option.disabled ? 'text-slate-400' : isSelected ? 'text-violet-700' : 'text-violet-600'
                  }`} />
                </div>
                <div className="mt-4 flex-1">
                  <p className={`text-base font-semibold ${
                    option.disabled ? 'text-slate-500' : 'text-slate-900'
                  }`}>
                    {t(`dataEntry.${option.key}`)}
                  </p>
                  <p className={`mt-1 text-sm leading-relaxed ${
                    option.disabled ? 'text-slate-400' : 'text-slate-600'
                  }`}>
                    {t(`dataEntry.${option.key}Desc`)}
                  </p>
                </div>
                {!option.disabled && isSelected && (
                  <CheckCircleIcon className="absolute bottom-1 right-1 h-7 w-7 text-violet-600" />
                )}
              </div>
            </button>
          );
        })}
      </div>

      <div className="flex items-center justify-between pt-2">
        <Button
          variant="ghost"
          onClick={onBack}
          className="rounded-xl border border-slate-200 bg-white px-5 text-slate-700 hover:border-violet-200 hover:bg-violet-50"
        >
          {t('back')}
        </Button>
        <Button
          onClick={onNext}
          disabled={!value || isSubmitting}
          loading={isSubmitting}
          className="rounded-xl bg-violet-600 px-6 text-white shadow-[0_12px_25px_-15px_rgba(124,58,237,1)] hover:bg-violet-700"
        >
          {t('finish')}
        </Button>
      </div>
    </div>
  );
}
