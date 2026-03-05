'use client';

import Link from 'next/link';
import { ArrowLeftIcon } from '@heroicons/react/24/outline';
import { Button } from '@/components/ui/button';
import { useTranslations } from 'next-intl';
import { cn } from '@/lib/utils';

interface BackButtonProps {
  href?: string;
  onClick?: () => void;
  label: string;
  variant?: 'button' | 'link';
  className?: string;
}

export function BackButton({ href, onClick, label, variant = 'button', className }: BackButtonProps) {
  const tCommon = useTranslations('common');

  const icon = <ArrowLeftIcon className="w-4 h-4" />;
  const desktopText = <span className="hidden sm:inline">{label}</span>;
  const mobileText = <span className="sm:hidden">{tCommon('back')}</span>;

  if (variant === 'link') {
    if (onClick) {
      return (
        <button
          type="button"
          onClick={onClick}
          className={cn('inline-flex items-center gap-2 text-sm text-slate-600 hover:text-slate-900 mb-4', className)}
        >
          {icon}
          {desktopText}
          {mobileText}
        </button>
      );
    }

    return (
      <Link
        href={href!}
        className={cn('inline-flex items-center gap-2 text-sm text-slate-600 hover:text-slate-900 mb-4', className)}
      >
        {icon}
        {desktopText}
        {mobileText}
      </Link>
    );
  }

  if (onClick) {
    return (
      <Button variant="secondary" size="sm" className={cn('flex items-center gap-2', className)} onClick={onClick}>
        {icon}
        {desktopText}
        {mobileText}
      </Button>
    );
  }

  return (
    <Link href={href!}>
      <Button variant="secondary" size="sm" className={cn('flex items-center gap-2', className)}>
        {icon}
        {desktopText}
        {mobileText}
      </Button>
    </Link>
  );
}
