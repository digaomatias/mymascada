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

export function StepDataEntry({ value, onChange, onNext, onBack }: StepDataEntryProps) {
  const t = useTranslations('onboarding');

  return (
    <div className="space-y-6 py-4">
      {/* Gradient accent strip */}
      <div className="h-1 w-full rounded-full bg-gradient-to-r from-primary-600 via-fuchsia-500 to-primary-600" />

      <div className="space-y-1">
        <h2 className="text-xl font-bold text-slate-900">
          {t('dataEntry.title')}
        </h2>
        <p className="text-slate-600">
          {t('dataEntry.subtitle')}
        </p>
      </div>

      <div className="space-y-3">
        {DATA_ENTRY_OPTIONS.map((option) => {
          const Icon = option.icon;
          const isSelected = value === option.key;

          return (
            <button
              type="button"
              key={option.key}
              className={`relative w-full rounded-xl border p-4 text-left transition-all ${
                option.disabled
                  ? 'cursor-not-allowed border-slate-200 bg-slate-50 opacity-60'
                  : isSelected
                    ? 'border-primary-500 ring-4 ring-primary-200/60 bg-primary-50/30 hover:shadow-md'
                    : 'border-slate-200 bg-white hover:-translate-y-0.5 hover:shadow-md'
              }`}
              onClick={() => {
                if (!option.disabled) {
                  onChange(option.key);
                }
              }}
              disabled={option.disabled}
            >
              {/* Recommended pill */}
              {option.recommended && !option.disabled && (
                <span className="absolute top-3 right-3 rounded-full bg-primary-600 px-2 py-0.5 text-xs font-semibold text-white">
                  {t('dataEntry.recommended')}
                </span>
              )}

              {/* Coming soon badge for disabled */}
              {option.disabled && (
                <span className="absolute top-3 right-3 rounded-full bg-slate-200 px-2 py-0.5 text-xs font-medium text-slate-500">
                  {t('dataEntry.bankDisabled')}
                </span>
              )}

              <div className="flex items-center gap-4">
                {/* Icon badge */}
                <div className={`w-11 h-11 rounded-xl grid place-items-center shrink-0 ${
                  option.disabled
                    ? 'bg-slate-100 ring-1 ring-slate-200'
                    : 'bg-primary-50 ring-1 ring-primary-100'
                }`}>
                  <Icon className={`w-6 h-6 ${
                    option.disabled ? 'text-slate-400' : 'text-primary-700'
                  }`} />
                </div>

                <div className="flex-1 min-w-0">
                  <p className={`font-medium ${
                    option.disabled ? 'text-slate-400' : 'text-slate-900'
                  }`}>
                    {t(`dataEntry.${option.key}`)}
                  </p>
                  <p className={`text-sm ${
                    option.disabled ? 'text-slate-300' : 'text-slate-500'
                  }`}>
                    {t(`dataEntry.${option.key}Desc`)}
                  </p>
                </div>

                {/* Checkmark for selected */}
                {!option.disabled && isSelected && (
                  <div className="w-6 h-6 shrink-0">
                    <CheckCircleIcon className="w-6 h-6 text-primary-600" />
                  </div>
                )}
              </div>
            </button>
          );
        })}
      </div>

      <div className="flex justify-between pt-4">
        <Button variant="ghost" onClick={onBack}>
          {t('back')}
        </Button>
        <Button onClick={onNext} disabled={!value}>
          {t('next')}
        </Button>
      </div>
    </div>
  );
}
