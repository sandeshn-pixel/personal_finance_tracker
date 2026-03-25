import { SelectField } from "./SelectField";
import type { SharedAccessView } from "../lib/sharedAccessView";

type WorkspaceScopeSelectProps = {
  value: SharedAccessView;
  onChange: (value: SharedAccessView) => void;
  className?: string;
  label?: string;
};

export function WorkspaceScopeSelect({ value, onChange, className = "", label = "Workspace view" }: WorkspaceScopeSelectProps) {
  return (
    <label className={className ? `${className} workspace-scope-select-wrap` : "workspace-scope-select-wrap"}>
      <span className="topbar-scope-label">{label}</span>
      <SelectField
        className="select-input workspace-scope-select"
        value={value}
        onChange={(event) => onChange(event.target.value as SharedAccessView)}
        aria-label={label}
      >
        <option value="all">All</option>
        <option value="mine">Mine</option>
        <option value="shared">Shared with me</option>
      </SelectField>
    </label>
  );
}
