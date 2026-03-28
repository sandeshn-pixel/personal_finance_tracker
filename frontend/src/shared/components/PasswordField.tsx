import type { InputHTMLAttributes, Ref } from "react";
import { useId, useState } from "react";

type PasswordFieldProps = InputHTMLAttributes<HTMLInputElement> & {
  inputRef?: Ref<HTMLInputElement>;
  toggleLabel?: string;
};

export function PasswordField({ inputRef, toggleLabel = "password", ...props }: PasswordFieldProps) {
  const [visible, setVisible] = useState(false);
  const inputId = useId();

  return (
    <div className="password-field">
      <input
        {...props}
        ref={inputRef}
        id={props.id ?? inputId}
        type={visible ? "text" : "password"}
        className={`password-field__input${props.className ? ` ${props.className}` : ""}`}
      />
      <button
        type="button"
        className="password-field__toggle"
        onClick={() => setVisible((current) => !current)}
        aria-label={`${visible ? "Hide" : "Show"} ${toggleLabel}`}
        aria-pressed={visible}
      >
        {visible ? "Hide" : "Show"}
      </button>
    </div>
  );
}
