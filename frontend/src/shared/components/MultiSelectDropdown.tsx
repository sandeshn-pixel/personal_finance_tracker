import { useEffect, useRef, useState } from "react";

export type MultiSelectDropdownProps = {
  label?: string;
  options: string[];
  selectedValues: string[];
  placeholder: string;
  onChange: (values: string[]) => void;
};

export function MultiSelectDropdown({ options, selectedValues, placeholder, onChange }: MultiSelectDropdownProps) {
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    function handlePointerDown(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }

    document.addEventListener("mousedown", handlePointerDown);
    return () => document.removeEventListener("mousedown", handlePointerDown);
  }, []);

  const summary = selectedValues.length === 0
    ? placeholder
    : selectedValues.length <= 2
      ? selectedValues.join(", ")
      : `${selectedValues.slice(0, 2).join(", ")} +${selectedValues.length - 2}`;

  function toggleValue(option: string) {
    if (selectedValues.includes(option)) {
      onChange(selectedValues.filter((item) => item !== option));
      return;
    }

    onChange([...selectedValues, option]);
  }

  return (
    <div ref={containerRef} className={`multi-select${isOpen ? " multi-select--open" : ""}`}>
      <button type="button" className="multi-select__trigger" onClick={() => setIsOpen((current) => !current)} aria-haspopup="listbox" aria-expanded={isOpen}>
        <span>{summary}</span>
        <span className="multi-select__caret" aria-hidden="true">v</span>
      </button>
      {isOpen ? (
        <div className="multi-select__panel" role="listbox" aria-multiselectable="true">
          {options.length === 0 ? (
            <p className="multi-select__empty">No tag suggestions yet.</p>
          ) : (
            options.map((option) => {
              const selected = selectedValues.includes(option);
              return (
                <label key={option} className={`multi-select__option${selected ? " multi-select__option--selected" : ""}`}>
                  <input type="checkbox" checked={selected} onChange={() => toggleValue(option)} />
                  <span>{option}</span>
                </label>
              );
            })
          )}
        </div>
      ) : null}
    </div>
  );
}