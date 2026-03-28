import '@testing-library/jest-dom/vitest'
import { vi } from 'vitest'

// Mock next-intl with actual translations
vi.mock('next-intl', () => {
  const path = require('path')
  const fs = require('fs')
  const messages = JSON.parse(
    fs.readFileSync(path.join(process.cwd(), 'messages', 'en.json'), 'utf-8')
  )

  function getNestedValue(obj: any, keyPath: string): any {
    return keyPath.split('.').reduce((acc: any, key: string) => acc?.[key], obj)
  }

  function interpolate(template: string, params?: Record<string, any>): string {
    if (!params || typeof template !== 'string') return template
    return template.replace(/\{(\w+)\}/g, (match, key) => {
      return params[key] !== undefined ? String(params[key]) : match
    })
  }

  return {
    useTranslations: (namespace: string) => {
      const section = getNestedValue(messages, namespace)
      return (key: string, params?: Record<string, any>): string => {
        const value = getNestedValue(section, key)
        if (typeof value !== 'string') return `${namespace}.${key}`
        return interpolate(value, params)
      }
    },
    useLocale: () => 'en',
    useMessages: () => messages,
    NextIntlClientProvider: ({ children }: any) => children,
  }
})

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
  CheckCircleIcon: () => null,
  XMarkIcon: () => null,
  ExclamationTriangleIcon: () => null,
  InformationCircleIcon: () => null,
  ArrowLeftIcon: () => null,
  DocumentCheckIcon: () => null,
  ChevronDownIcon: () => null,
  ChevronUpIcon: () => null,
  ArrowPathIcon: () => null,
  EllipsisHorizontalIcon: () => null,
  DocumentDuplicateIcon: () => null,
  ClockIcon: () => null,
}))

// Mock UI components
vi.mock('@/components/ui/button', () => {
  const React = require('react')
  return {
    Button: ({ children, disabled, onClick, className, type, ...rest }: any) => {
      return React.createElement('button', { disabled: disabled || undefined, onClick, className, type }, children)
    },
  }
})

vi.mock('@/components/ui/card', () => {
  const React = require('react')
  return {
    Card: ({ children, className, ...rest }: any) => React.createElement('div', { className, 'data-testid': 'card' }, children),
    CardContent: ({ children, className, ...rest }: any) => React.createElement('div', { className }, children),
    CardHeader: ({ children, className, onClick, ...rest }: any) => React.createElement('div', { className, onClick }, children),
    CardTitle: ({ children, className, ...rest }: any) => React.createElement('div', { className }, children),
  }
})

vi.mock('@/components/ui/badge', () => {
  const React = require('react')
  return {
    Badge: ({ children, className, ...rest }: any) => React.createElement('span', { className }, children),
  }
})

vi.mock('@/components/ui/confidence-indicator', () => ({
  ConfidenceIndicator: () => null,
}))

vi.mock('@/lib/utils', () => ({
  formatCurrency: (amount: number) => `$${amount.toFixed(2)}`,
  formatDate: (date: string) => new Date(date).toLocaleDateString(),
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
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
})
