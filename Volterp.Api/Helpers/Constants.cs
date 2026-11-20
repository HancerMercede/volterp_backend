namespace Volterp.Api.Helpers;

public static class Constants
{
  public const string Description =
    @"<p>Backend API for <b>Volterp ERP</b> — a multi-tenant ERP built on Clean Architecture (.NET 10 + PostgreSQL).</p>
      <h2>Authentication</h2>
        <p>JWT Bearer tokens. Call <code>POST /api/auth/login</code> to obtain a token containing these claims:</p>
        <ul>
          <li><code>name</code> — username</li>
          <li><code>email</code> — user's email</li>
          <li><code>role</code> — one claim per role the user holds (<code>Admin</code>, <code>SuperAdmin</code>, etc.). The permissions of the user are the union of the permissions of its roles.</li>
          <li><code>companyId</code> — tenant identifier (every request is implicitly scoped to this tenant)</li>
          <li><code>userId</code> — current user identifier (populates <code>CreatedBy</code> / <code>UpdatedBy</code> audit fields automatically)</li>
        </ul>
        <p>Click <b>Authorize</b> at the top and paste <code>Bearer {token}</code> to authenticate subsequent requests.</p>

        <h2>Multi-tenancy</h2>
        <p>All endpoints are scoped to the <code>companyId</code> claim. Cross-tenant access is blocked at the controller and service layer. If you see data from another tenant, that's a bug — please report it.</p>

        <h2>Conventions</h2>
        <ul>
          <li><b>Pagination</b> — list endpoints accept <code>?pageNumber=1&amp;pageSize=20</code> and return <code>PagedResult&lt;T&gt;</code> with <code>TotalCount</code>, <code>PageNumber</code>, <code>PageSize</code>, <code>TotalPages</code>, and an <code>Items</code> array.</li>
          <li><b>Errors</b> — <code>ErrorResponse { Error, Details? }</code> on 4xx validation, RFC 7807 <code>ProblemDetails</code> on 5xx and rate limiting.</li>
          <li><b>Status codes</b> — <code>200 OK</code> success, <code>201 Created</code> on create with <code>Location</code> header, <code>204 No Content</code> on delete, <code>400 Bad Request</code> validation, <code>401 Unauthorized</code> no/invalid token, <code>403 Forbidden</code> role check failed, <code>404 Not Found</code> resource missing or out of tenant scope, <code>429 Too Many Requests</code> with <code>Retry-After</code> header.</li>
          <li><b>Casing</b> — JSON is camelCase. <b>DateTime</b> — all timestamps UTC, ISO 8601.</li>
        </ul>

        <h2>Audit fields</h2>
        <p>Most entities inherit from <code>AuditEntity</code> and carry <code>CreatedAt</code>, <code>UpdatedAt</code>, <code>CreatedBy</code>, <code>UpdatedBy</code>. These populate automatically from the JWT <code>userId</code> claim on create/update.</p>

        <h2>Health & diagnostics</h2>
        <ul>
          <li><code>GET /health</code> — overall status + per-check breakdown.</li>
          <li><code>GET /_diagnostics/exceptions</code> — last 50 logged exceptions.</li>
        </ul>";
}