/** GET api/account/userInfo */
export interface UserProfile {
  email: string;
  userName: string;
  firstName: string;
  lastName: string;
  profilePhotoUrl: string;
}

/** PUT api/account/update-User-Info */
export interface UpdateProfilePayload {
  firstName: string;
  lastName: string;
}

/** PUT api/account/change-password — matches ChangePasswordRequest JSON */
export interface ChangePasswordPayload {
  currentPassword: string;
  newPassword: string;
}

/** GET api/users */
export interface AdminUserRow {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  isDisabled: boolean;
  roles: string[];
}

/** PUT api/users/{id} */
export interface AdminUpdateUserPayload {
  firstName: string;
  lastName: string;
  email: string;
  roles: string[];
}

/** GET api/roles */
export interface RoleSummary {
  id: string;
  name: string;
  isDeleted: boolean;
}

/** GET api/roles/{id} */
export interface RoleDetail {
  id: string;
  name: string;
  isDeleted: boolean;
  permissions: string[];
}

/** POST/PUT api/roles — matches RoleRequest JSON */
export interface RoleUpsertPayload {
  name: string;
  permissions: string[];
}
