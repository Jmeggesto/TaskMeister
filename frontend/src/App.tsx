import { useAuth } from "./hooks/useAuth";
import { LandingPage } from "./pages/LandingPage";
import { TodosPage } from "./pages/TodosPage";
import "./App.css";

function App() {
  const { auth, signup, login, logout } = useAuth();

  if (!auth) {
    return <LandingPage onSignup={signup} onLogin={login} />;
  }

  return <TodosPage auth={auth} onLogout={logout} />;
}

export default App;
