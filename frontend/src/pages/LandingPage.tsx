import { useState } from "react";

interface LandingPageProps {
  onSignup: (name: string, email: string, password: string) => Promise<void>;
  onLogin: (email: string, password: string) => Promise<void>;
}

type Mode = "login" | "signup";

export function LandingPage({ onSignup, onLogin }: LandingPageProps) {
  const [mode, setMode] = useState<Mode>("login");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const isSignup = mode === "signup";

  function toggleMode() {
    setMode(isSignup ? "login" : "signup");
    setError(null);
  }

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setLoading(true);

    const form = new FormData(e.currentTarget);
    const email = form.get("email") as string;
    const password = form.get("password") as string;

    try {
      if (isSignup) {
        const name = form.get("name") as string;
        await onSignup(name, email, password);
      } else {
        await onLogin(email, password);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="landing">
      <div className="landing-card">
        <h1 className="landing-title">TaskMeister</h1>
        <p className="landing-subtitle">{isSignup ? "Create an account" : "Welcome back"}</p>

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}

        <form onSubmit={handleSubmit} className="auth-form">
          {isSignup && (
            <label className="field">
              <span>Name</span>
              <input name="name" type="text" required minLength={2} maxLength={50} autoComplete="name" />
            </label>
          )}

          <label className="field">
            <span>Email</span>
            <input name="email" type="email" required autoComplete="email" />
          </label>

          <label className="field">
            <span>Password</span>
            <input
              name="password"
              type="password"
              required
              minLength={8}
              autoComplete={isSignup ? "new-password" : "current-password"}
            />
          </label>

          <button type="submit" className="btn-primary" disabled={loading}>
            {loading ? "…" : isSignup ? "Sign up" : "Log in"}
          </button>
        </form>

        <p className="mode-toggle">
          {isSignup ? "Already have an account?" : "Don't have an account?"}{" "}
          <button className="btn-link" onClick={toggleMode}>
            {isSignup ? "Log in" : "Sign up"}
          </button>
        </p>
      </div>
    </div>
  );
}
