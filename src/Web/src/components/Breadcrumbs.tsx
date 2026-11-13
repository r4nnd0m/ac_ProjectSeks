// Breadcrumbs-Komponente für die Navigation
import { Link } from 'react-router-dom';

interface BreadcrumbItem {
  label: string;
  to?: string;
}

interface BreadcrumbsProps {
  items: BreadcrumbItem[];
}

export function Breadcrumbs({ items }: BreadcrumbsProps) {
  if (items.length === 0) return null;

  return (
    <nav className="breadcrumbs" aria-label=" breadcrumbs">
      <div className="breadcrumb-list">
        {items.map((item, index) => (
          <span key={index} className={index === items.length - 1 ? 'breadcrumb-current' : 'breadcrumb-link'}>
            {item.to ? <Link to={item.to}>{item.label}</Link> : item.label}
            {index < items.length - 1 && <span className="breadcrumb-separator"> › </span>}
          </span>
        ))}
      </div>
    </nav>
  );
}
