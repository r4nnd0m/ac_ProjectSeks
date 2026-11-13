interface BrandLogoProps {
  size?: number;
}

export function BrandLogo({ size = 40 }: BrandLogoProps) {
  return <img src="/logo.jpg" alt="CMSLPD" className="brand-logo" style={{ width: size, height: size }} />;
}
