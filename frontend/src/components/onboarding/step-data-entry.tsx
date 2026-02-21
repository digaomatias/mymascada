'use client';

import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

interface StepDataEntryProps {
  value: string;
  onChange: (method: string) => void;
  onNext: () => void;
  onBack: () => void;
}

const DATA_ENTRY_OPTIONS = [
  { key: 'manual', icon: '📝', disabled: false },
  { key: 'csv', icon: '📄', disabled: false },
  { key: 'bank', icon: '🏦', disabled: true },
] as const;

export function StepDataEntry({ value, onChange, onNext, onBack }: StepDataEntryProps) {
  const t = useTranslations('onboarding');

  return (
    <div className="space-y-6 py-4">
      <div className="text-center space-y-2">
        <h2 className="text-xl font-bold text-gray-900">
          {t('dataEntry.title')}
        </h2>
        <p className="text-gray-600">
          {t('dataEntry.subtitle')}
        </p>
      </div>

      <div className="space-y-3">
        {DATA_ENTRY_OPTIONS.map((option) => (
          <Card
            key={option.key}
            className={`cursor-pointer transition-all ${
              option.disabled
                ? 'opacity-50 cursor-not-allowed'
                : value === option.key
                  ? 'ring-2 ring-primary-600 border-primary-600'
                  : 'hover:border-gray-400'
            }`}
            onClick={() => {
              if (!option.disabled) {
                onChange(option.key);
              }
            }}
          >
            <CardContent className="flex items-center gap-4 py-4">
              <span className="text-2xl">{option.icon}</span>
              <div className="flex-1">
                <p className="font-medium text-gray-900">
                  {t(`dataEntry.${option.key}`)}
                </p>
                <p className="text-sm text-gray-500">
                  {t(`dataEntry.${option.key}Desc`)}
                </p>
              </div>
              {option.disabled && (
                <span className="text-xs bg-gray-100 text-gray-500 px-2 py-1 rounded-full">
                  {t('dataEntry.bankDisabled')}
                </span>
              )}
              {!option.disabled && value === option.key && (
                <div className="w-5 h-5 rounded-full bg-primary-600 flex items-center justify-center">
                  <svg className="w-3 h-3 text-white" fill="currentColor" viewBox="0 0 20 20">
                    <path
                      fillRule="evenodd"
                      d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
                      clipRule="evenodd"
                    />
                  </svg>
                </div>
              )}
            </CardContent>
          </Card>
        ))}
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
