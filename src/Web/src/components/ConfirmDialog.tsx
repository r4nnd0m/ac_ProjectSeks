// Bestätigungsdialog als Modal (Einsatz für Löschen, Deaktivieren etc.)

interface ConfirmDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  variant?: 'danger' | 'primary';
}

export function ConfirmDialog({
  isOpen,
  onClose,
  onConfirm,
  title,
  message,
  confirmText = 'Löschen',
  cancelText = 'Abbrechen',
  variant = 'danger',
}: ConfirmDialogProps) {
  const handleConfirm = () => {
    onConfirm();
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="backdrop-blur">
      <div className="backdrop" onClick={onClose} />
      <div className="confirm-dialog">
        <h3 className="confirm-title">{title}</h3>
        <p className="confirm-message">{message}</p>
        <div className="confirm-actions">
          <button
            type="button"
            className="link-btn"
            onClick={onClose}
          >
            {cancelText}
          </button>
          <button
            type="button"
            className={`danger ${variant === 'primary' ? 'secondary' : ''}`}
            onClick={handleConfirm}
          >
            {confirmText}
          </button>
        </div>
      </div>
    </div>
  );
}
