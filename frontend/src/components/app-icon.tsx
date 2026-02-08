import Image from 'next/image';

interface AppIconProps {
  size?: number;
  className?: string;
}

export function AppIcon({ size = 48, className }: AppIconProps) {
  return (
    <Image
      src="/icon.png"
      alt="MyMascada"
      width={size}
      height={size}
      className={className}
    />
  );
}
