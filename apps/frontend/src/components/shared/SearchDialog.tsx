/**
 * KI-søk dialog — opens as a modal overlay with a search field.
 *
 * Uses @digdir/designsystemet-react Dialog + Search components.
 * Currently a UI shell — search results will be integrated when
 * the Eira/Benjamin API is available.
 *
 * Opened via:
 *  - Search icon button in the header
 *  - Ctrl+K / Cmd+K keyboard shortcut
 */
import { useEffect, useRef, useState } from 'react';
import { Dialog, Search, Button, Link, Heading, Paragraph } from '@digdir/designsystemet-react';

export default function SearchDialog() {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [mode, setMode] = useState<'idle' | 'searching' | 'results'>('idle');
  const dialogRef = useRef<HTMLDialogElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  // Open/close
  function openDialog() {
    setOpen(true);
    setQuery('');
    setMode('idle');
  }

  function closeDialog() {
    setOpen(false);
    setQuery('');
    setMode('idle');
  }

  // Keyboard shortcut: Cmd+K / Ctrl+K
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        if (open) {
          closeDialog();
        } else {
          openDialog();
        }
      }
    }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open]);

  // Listen for external trigger (header button)
  useEffect(() => {
    function handleTrigger() {
      openDialog();
    }
    window.addEventListener('open-search-dialog', handleTrigger);
    return () => window.removeEventListener('open-search-dialog', handleTrigger);
  }, []);

  // Focus input when dialog opens
  useEffect(() => {
    if (open) {
      // Small delay to let dialog render
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [open]);

  function handleSearch(e?: React.FormEvent) {
    e?.preventDefault();
    if (!query.trim()) return;
    setMode('searching');
    // TODO: Call Eira/Benjamin search API here
    // For now, simulate a brief loading then show placeholder results
    setTimeout(() => setMode('results'), 600);
  }

  if (!open) return null;

  return (
    <div className="search-dialog-backdrop" onClick={(e) => {
      if (e.target === e.currentTarget) closeDialog();
    }}>
      <div className="search-dialog-container">
        <div className="search-dialog-content">
          <form onSubmit={handleSearch} className="search-dialog-form">
            <Search
              className="search-dialog-search"
              data-size="lg"
            >
              <Search.Input
                ref={inputRef}
                aria-label="Søk på ki.norge.no"
                placeholder="Søk etter KI-begreper, veiledning, eksempler..."
                value={query}
                onChange={(e) => setQuery(e.target.value)}
              />
              {query && <Search.Clear onClick={() => { setQuery(''); setMode('idle'); }} />}
              <Search.Button onClick={handleSearch} />
            </Search>
          </form>

          {mode === 'idle' && query === '' && (
            <div className="search-dialog-hints">
              <Paragraph data-size="sm" className="search-hint-text">
                Tips: Bruk <kbd>Ctrl+K</kbd> for å åpne søk raskt
              </Paragraph>
              <div className="search-suggestions">
                <span className="search-suggestions-label">Forslag:</span>
                {['Kunstig intelligens', 'Sandkasse', 'Veiledning', 'KI-ordbok'].map((term) => (
                  <button
                    key={term}
                    type="button"
                    className="search-suggestion-chip"
                    onClick={() => { setQuery(term); handleSearch(); }}
                  >
                    {term}
                  </button>
                ))}
              </div>
            </div>
          )}

          {mode === 'searching' && (
            <div className="search-dialog-loading">
              <Paragraph>Søker...</Paragraph>
            </div>
          )}

          {mode === 'results' && (
            <div className="search-dialog-results">
              <Paragraph data-size="sm" className="search-results-info">
                Søkeresultater for &laquo;{query}&raquo; — API ikke tilkoblet ennå
              </Paragraph>
              <div className="search-result-placeholder">
                <Paragraph data-size="sm">
                  Søkeresultater vil vises her når Eira/Benjamin-API-et er integrert.
                </Paragraph>
              </div>
            </div>
          )}
        </div>

        <button
          type="button"
          className="search-dialog-close"
          onClick={closeDialog}
          aria-label="Lukk søk"
        >
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <line x1="18" y1="6" x2="6" y2="18"/>
            <line x1="6" y1="6" x2="18" y2="18"/>
          </svg>
        </button>
      </div>
    </div>
  );
}
