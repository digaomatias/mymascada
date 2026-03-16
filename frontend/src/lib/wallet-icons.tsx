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
  label: string;
}

export const WALLET_ICONS: WalletIconOption[] = [
  { id: 'banknotes', icon: BanknotesIcon, label: 'Money' },
  { id: 'credit-card', icon: CreditCardIcon, label: 'Card' },
  { id: 'building-library', icon: BuildingLibraryIcon, label: 'Bank' },
  { id: 'home', icon: HomeIcon, label: 'Home' },
  { id: 'academic-cap', icon: AcademicCapIcon, label: 'Education' },
  { id: 'globe-alt', icon: GlobeAltIcon, label: 'Travel' },
  { id: 'heart', icon: HeartIcon, label: 'Health' },
  { id: 'shopping-bag', icon: ShoppingBagIcon, label: 'Shopping' },
  { id: 'gift', icon: GiftIcon, label: 'Gift' },
  { id: 'truck', icon: TruckIcon, label: 'Transport' },
  { id: 'device-phone-mobile', icon: DevicePhoneMobileIcon, label: 'Phone' },
  { id: 'computer-desktop', icon: ComputerDesktopIcon, label: 'Tech' },
  { id: 'musical-note', icon: MusicalNoteIcon, label: 'Music' },
  { id: 'book-open', icon: BookOpenIcon, label: 'Books' },
  { id: 'sparkles', icon: SparklesIcon, label: 'Special' },
  { id: 'shield-check', icon: ShieldCheckIcon, label: 'Insurance' },
  { id: 'wrench-screwdriver', icon: WrenchScrewdriverIcon, label: 'Repair' },
  { id: 'cake', icon: CakeIcon, label: 'Birthday' },
  { id: 'user-group', icon: UserGroupIcon, label: 'Family' },
  { id: 'star', icon: StarIcon, label: 'Savings' },
  { id: 'rocket-launch', icon: RocketLaunchIcon, label: 'Goals' },
  { id: 'puzzle-piece', icon: PuzzlePieceIcon, label: 'Hobbies' },
  { id: 'fire', icon: FireIcon, label: 'Emergency' },
  { id: 'bolt', icon: BoltIcon, label: 'Utilities' },
  { id: 'currency-dollar', icon: CurrencyDollarIcon, label: 'General' },
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
