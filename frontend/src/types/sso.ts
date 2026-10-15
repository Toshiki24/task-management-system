/** SSO(OIDC)接続設定(M5 §7、System Admin 向け)。 */
export interface SsoConnection {
  id: number;
  name: string;
  protocol: string;
  issuer: string;
  clientId: string;
  scopes: string;
  autoProvision: boolean;
  provisionWorkspaceId: number | null;
  provisionRole: string | null;
  isEnabled: boolean;
  /** クライアントシークレット参照が設定されているか(値そのものは返さない)。 */
  hasSecret: boolean;
}
