import React from 'react'
import '@testing-library/jest-dom/vitest'
import { vi } from 'vitest'
import messages from './messages/en.json'

// Mock Next.js router
vi.mock('next/navigation', () => ({
  useRouter() {
    return {
      push: vi.fn(),
      replace: vi.fn(),
      prefetch: vi.fn(),
      back: vi.fn(),
      forward: vi.fn(),
      refresh: vi.fn(),
    }
  },
  useSearchParams() {
    return new URLSearchParams()
  },
  usePathname() {
    return ''
  },
}))

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
  },
}))

// Mock Heroicons
vi.mock('@heroicons/react/24/outline', () => ({
  CheckCircleIcon: vi.fn(() => null),
  XMarkIcon: vi.fn(() => null),
  ExclamationTriangleIcon: vi.fn(() => null),
  InformationCircleIcon: vi.fn(() => null),
  ArrowLeftIcon: vi.fn(() => null),
  DocumentCheckIcon: vi.fn(() => null),
  ChevronDownIcon: vi.fn(() => null),
  ChevronUpIcon: vi.fn(() => null),
  ArrowPathIcon: vi.fn(() => null),
  EllipsisHorizontalIcon: vi.fn(() => null),
  DocumentDuplicateIcon: vi.fn(() => null),
  ClockIcon: vi.fn(() => null),
  ShieldCheckIcon: vi.fn(() => null),
  ArrowsRightLeftIcon: vi.fn(() => null),
  PencilSquareIcon: vi.fn(() => null),
  QuestionMarkCircleIcon: vi.fn(() => null),
}))

// Mock UI components  
vi.mock('@/components/ui/button', () => ({
  Button: vi.fn(({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement> & { variant?: string; size?: string; asChild?: boolean }) => {
    const { variant, size, asChild, ...htmlProps } = props as any
    return React.createElement('button', htmlProps, children)
  }),
}))

vi.mock('@/components/ui/card', () => ({
  Card: vi.fn(({ children, ...props }: React.HTMLAttributes<HTMLDivElement>) =>
    React.createElement('div', props, children)
  ),
  CardContent: vi.fn(({ children, ...props }: React.HTMLAttributes<HTMLDivElement>) =>
    React.createElement('div', props, children)
  ),
  CardHeader: vi.fn(({ children, ...props }: React.HTMLAttributes<HTMLDivElement>) =>
    React.createElement('div', props, children)
  ),
  CardTitle: vi.fn(({ children, ...props }: React.HTMLAttributes<HTMLDivElement>) =>
    React.createElement('div', props, children)
  ),
}))

vi.mock('@/components/ui/badge', () => ({
  Badge: vi.fn(({ children, ...props }: React.HTMLAttributes<HTMLSpanElement> & { variant?: string }) => {
    const { variant, ...htmlProps } = props as any
    return React.createElement('span', htmlProps, children)
  }),
}))

vi.mock('@/components/ui/confidence-indicator', () => ({
  ConfidenceIndicator: vi.fn(() => 'Confidence Indicator'),
}))

vi.mock('@/lib/utils', () => ({
  formatCurrency: (amount: number) => `$${amount.toFixed(2)}`,
  formatDate: (date: string) => new Date(date).toLocaleDateString(),
}))

// Mock next-intl with real English messages
function getNestedValue(obj: Record<string, unknown>, path: string): unknown {
  return path.split('.').reduce((acc: unknown, part: string) => {
    if (acc && typeof acc === 'object') return (acc as Record<string, unknown>)[part]
    return undefined
  }, obj)
}

vi.mock('next-intl', () => ({
  useTranslations: (namespace?: string) => {
    const section = namespace
      ? getNestedValue(messages as Record<string, unknown>, namespace)
      : messages
    return (key: string, params?: Record<string, string | number>) => {
      let value = getNestedValue(
        (section || {}) as Record<string, unknown>,
        key
      )
      if (typeof value !== 'string') return key
      if (params) {
        Object.entries(params).forEach(([k, v]) => {
          value = (value as string).replace(`{${k}}`, String(v))
        })
      }
      return value
    }
  },
  useLocale: () => 'en',
  useMessages: () => messages,
  NextIntlClientProvider: ({ children }: { children: unknown }) => children,
}))

// Global test setup
global.ResizeObserver = vi.fn().mockImplementation(() => ({
  observe: vi.fn(),
  unobserve: vi.fn(),
  disconnect: vi.fn(),
}))

// Mock window.matchMedia
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation(query => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(), // deprecated
    removeListener: vi.fn(), // deprecated
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
})