/** Mirrors backend Models/Responses/AuthResponse.cs */
export interface AuthResponse {
  token: string;
  name: string;
  expiresAt: string; // ISO 8601 date string (JSON-serialised DateTime)
}
