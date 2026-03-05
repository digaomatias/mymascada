'use client';

import Link from 'next/link';
import { ArrowLeftIcon } from '@heroicons/react/24/outline';
import { Button } from '@/components/ui/button';
import { useTranslations } from 'next-intl';

interface BackButtonProps {
  href: string;
  label: string;
  variant?: 'button' | 'link';
  className?: string;
}

export function BackButton({ href, label, variant = 'button', className }: BackButtonProps) {
  const tCommon = useTranslations('common');

  const icon = <ArrowLeftIcon className="w-4 h-4" />;
  const desktopText = <span className="hidden sm:inline">{label}</span>;
  const mobileText = <span className="sm:hidden">{tCommon('back')}</span>;

  if (variant === 'link') {
    return (
      <Link
        href={href}
        className={`inline-flex items-center gap-2 text-sm text-slate-600 hover:text-slate-900 mb-4 ${className || ''}`}
      >
        {icon}
        {desktopText}
        {mobileText}
      </Link>
    );
  }

  return (
    <Link href={href}>
      <Button variant="secondary" size="sm" className={`flex items-center gap-2 ${className || ''}`}>
        {icon}
        {desktopText}
        {mobileText}
      </Button>
    </Link>
  );
}
