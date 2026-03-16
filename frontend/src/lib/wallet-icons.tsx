import {
  BanknotesIcon,
  CreditCardIcon,
  BuildingLibraryIcon,
  HomeIcon,
  AcademicCapIcon,
  GlobeAltIcon,
  HeartIcon,
  ShoppingBagIcon,
  GiftIcon,
  TruckIcon,
  DevicePhoneMobileIcon,
  ComputerDesktopIcon,
  MusicalNoteIcon,
  BookOpenIcon,
  SparklesIcon,
  ShieldCheckIcon,
  WrenchScrewdriverIcon,
  CakeIcon,
  UserGroupIcon,
  StarIcon,
  RocketLaunchIcon,
  PuzzlePieceIcon,
  FireIcon,
  BoltIcon,
  CurrencyDollarIcon,
} from '@heroicons/react/24/outline';
import React from 'react';

export interface WalletIconOption {
  id: string;
  icon: React.ComponentType<React.SVGProps<SVGSVGElement>>;
  labelKey: string;
}

export const WALLET_ICONS: WalletIconOption[] = [
  { id: 'banknotes', icon: BanknotesIcon, labelKey: 'money' },
  { id: 'credit-card', icon: CreditCardIcon, labelKey: 'card' },
  { id: 'building-library', icon: BuildingLibraryIcon, labelKey: 'bank' },
  { id: 'home', icon: HomeIcon, labelKey: 'home' },
  { id: 'academic-cap', icon: AcademicCapIcon, labelKey: 'education' },
  { id: 'globe-alt', icon: GlobeAltIcon, labelKey: 'travel' },
  { id: 'heart', icon: HeartIcon, labelKey: 'health' },
  { id: 'shopping-bag', icon: ShoppingBagIcon, labelKey: 'shopping' },
  { id: 'gift', icon: GiftIcon, labelKey: 'gift' },
  { id: 'truck', icon: TruckIcon, labelKey: 'transport' },
  { id: 'device-phone-mobile', icon: DevicePhoneMobileIcon, labelKey: 'phone' },
  { id: 'computer-desktop', icon: ComputerDesktopIcon, labelKey: 'tech' },
  { id: 'musical-note', icon: MusicalNoteIcon, labelKey: 'music' },
  { id: 'book-open', icon: BookOpenIcon, labelKey: 'books' },
  { id: 'sparkles', icon: SparklesIcon, labelKey: 'special' },
  { id: 'shield-check', icon: ShieldCheckIcon, labelKey: 'insurance' },
  { id: 'wrench-screwdriver', icon: WrenchScrewdriverIcon, labelKey: 'repair' },
  { id: 'cake', icon: CakeIcon, labelKey: 'birthday' },
  { id: 'user-group', icon: UserGroupIcon, labelKey: 'family' },
  { id: 'star', icon: StarIcon, labelKey: 'savings' },
  { id: 'rocket-launch', icon: RocketLaunchIcon, labelKey: 'goals' },
  { id: 'puzzle-piece', icon: PuzzlePieceIcon, labelKey: 'hobbies' },
  { id: 'fire', icon: FireIcon, labelKey: 'emergency' },
  { id: 'bolt', icon: BoltIcon, labelKey: 'utilities' },
  { id: 'currency-dollar', icon: CurrencyDollarIcon, labelKey: 'general' },
];

export const DEFAULT_WALLET_ICON_ID = 'currency-dollar';

export function getWalletIcon(iconId?: string): React.ComponentType<React.SVGProps<SVGSVGElement>> {
  if (!iconId) return CurrencyDollarIcon;
  const match = WALLET_ICONS.find((i) => i.id === iconId);
  return match?.icon || CurrencyDollarIcon;
}

export function WalletIcon({ iconId, className }: { iconId?: string; className?: string }) {
  const IconComponent = getWalletIcon(iconId);
  return <IconComponent className={className || 'h-6 w-6'} />;
}
