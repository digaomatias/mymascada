import '@testing-library/jest-dom'
import enMessages from './messages/en.json'

const getMessage = (messages, path) => {
  if (!path) return undefined
  return path.split('.').reduce((acc, key) => (acc ? acc[key] : undefined), messages)
}

// Mock Next.js router
jest.mock('next/navigation', () => ({
  useRouter() {
    return {
      push: jest.fn(),
      replace: jest.fn(),
      prefetch: jest.fn(),
      back: jest.fn(),
      forward: jest.fn(),
      refresh: jest.fn(),
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
jest.mock('sonner', () => ({
  toast: {
    success: jest.fn(),
    error: jest.fn(),
    warning: jest.fn(),
    info: jest.fn(),
  },
}))

// Mock next-intl translations with English messages
jest.mock('next-intl', () => ({
  useTranslations: (namespace) => (key, values) => {
    const fullKey = namespace ? `${namespace}.${key}` : key
    let message = getMessage(enMessages, fullKey)
    if (!message) return key
    if (values) {
      Object.entries(values).forEach(([valueKey, value]) => {
        message = message.replaceAll(`{${valueKey}}`, String(value))
      })
    }
    return message
  },
}))

// Mock Heroicons
jest.mock('@heroicons/react/24/outline', () => ({
  CheckCircleIcon: ({ className }) => <div className={className} data-testid="check-circle-icon" />,
  XMarkIcon: ({ className }) => <div className={className} data-testid="x-mark-icon" />,
  ExclamationTriangleIcon: ({ className }) => <div className={className} data-testid="exclamation-triangle-icon" />,
  InformationCircleIcon: ({ className }) => <div className={className} data-testid="information-circle-icon" />,
  ArrowLeftIcon: ({ className }) => <div className={className} data-testid="arrow-left-icon" />,
  DocumentCheckIcon: ({ className }) => <div className={className} data-testid="document-check-icon" />,
  ChevronDownIcon: ({ className }) => <div className={className} data-testid="chevron-down-icon" />,
  ChevronUpIcon: ({ className }) => <div className={className} data-testid="chevron-up-icon" />,
  ArrowPathIcon: ({ className }) => <div className={className} data-testid="arrow-path-icon" />,
  EllipsisHorizontalIcon: ({ className }) => <div className={className} data-testid="ellipsis-horizontal-icon" />,
}))

// Global test setup
global.ResizeObserver = jest.fn().mockImplementation(() => ({
  observe: jest.fn(),
  unobserve: jest.fn(),
  disconnect: jest.fn(),
}))

// Mock window.matchMedia
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: jest.fn().mockImplementation(query => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: jest.fn(), // deprecated
    removeListener: jest.fn(), // deprecated
    addEventListener: jest.fn(),
    removeEventListener: jest.fn(),
    dispatchEvent: jest.fn(),
  })),
})
