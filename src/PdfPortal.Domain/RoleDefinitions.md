# PDF Portal - User Roles & Permissions

## Overview

The PDF Portal system implements a role-based access control (RBAC) system with three distinct user roles. Each role has specific permissions and access levels to different features and data within the system.

## Role Hierarchy

```
Admin (Highest Privileges)
    ↓
Vendor (Document Processing)
    ↓
Client (Document Consumption)
```

---

## 🔧 **ADMIN Role**

### **Primary Responsibilities**
- **System Administration**: Complete control over the PDF Portal system
- **User Management**: Create and manage vendor accounts
- **Template Management**: Define and maintain PDF transformation rules
- **System Monitoring**: Oversee all document processing activities

### **Permissions & Access**

#### **User Management**
- ✅ **Create Vendor Accounts**: `POST /api/auth/register-vendor`
- ✅ **View All Users**: Access to user management interface
- ✅ **Manage User Roles**: Assign and modify user roles
- ✅ **User Account Management**: Enable/disable accounts

#### **Template Management**
- ✅ **Create Templates**: `POST /api/templates`
- ✅ **View All Templates**: `GET /api/templates`
- ✅ **Edit Templates**: `PUT /api/templates/{id}`
- ✅ **Delete Templates**: `DELETE /api/templates/{id}`
- ✅ **Template Validation**: Validate JSON rule definitions

#### **Document Management**
- ✅ **View All Documents**: `GET /api/documents/processed`
- ✅ **Download Any Document**: `GET /api/documents/processed/{id}/file`
- ✅ **Filter by Vendor**: Access to vendor-specific document filtering
- ✅ **Document Approval**: Approve/reject processed documents
- ✅ **Audit Trail**: Access to complete document processing history

#### **System Administration**
- ✅ **System Configuration**: Modify system settings
- ✅ **Database Management**: Access to database operations
- ✅ **Log Access**: View system logs and audit trails
- ✅ **Performance Monitoring**: Monitor system performance

### **Data Access**
- **Full Access**: Can view and modify all data in the system
- **Cross-Vendor Access**: Can access documents from any vendor
- **System-Wide Analytics**: Access to system-wide reports and metrics

---

## 📄 **VENDOR Role**

### **Primary Responsibilities**
- **Document Upload**: Upload PDF documents for processing
- **Document Processing**: Use templates to transform documents
- **Quality Control**: Preview and validate processed documents
- **Document Submission**: Submit final processed documents

### **Permissions & Access**

#### **Document Processing**
- ✅ **Upload Documents**: `POST /api/documents/upload`
- ✅ **Preview Processing**: Generate preview of transformed documents
- ✅ **Confirm Documents**: `POST /api/documents/confirm`
- ✅ **Template Selection**: Choose from available templates

#### **Document Management**
- ✅ **View Own Documents**: Access to their own uploaded documents
- ✅ **Document Status**: Track processing status of their documents
- ✅ **Retry Processing**: Re-process documents if needed

#### **Template Access**
- ✅ **View Available Templates**: Access to templates assigned to them
- ✅ **Template Information**: View template details and requirements

### **Data Access**
- **Own Data Only**: Can only access their own uploaded documents
- **Template Access**: Limited to templates assigned by admin
- **No Cross-Vendor Access**: Cannot view other vendors' documents

### **Restrictions**
- ❌ **Cannot Create Templates**: No access to template management
- ❌ **Cannot Manage Users**: No user management capabilities
- ❌ **Cannot View Other Vendors**: No access to other vendors' data
- ❌ **Cannot Access System Settings**: No administrative privileges

---

## 👥 **CLIENT Role**

### **Primary Responsibilities**
- **Document Consumption**: Access and download processed documents
- **Data Retrieval**: Extract structured data from processed documents
- **Document Search**: Find and filter documents based on criteria
- **Integration**: Integrate with external systems via API

### **Permissions & Access**

#### **Document Access**
- ✅ **View Processed Documents**: `GET /api/documents/processed`
- ✅ **Download Documents**: `GET /api/documents/processed/{id}/file`
- ✅ **Search Documents**: Filter documents by various criteria
- ✅ **Extract Data**: Access structured data from documents

#### **Data Access**
- ✅ **Structured Data**: Access to extracted JSON data
- ✅ **Document Metadata**: View document information and timestamps
- ✅ **Vendor Information**: View vendor details for documents

### **Data Access**
- **Read-Only Access**: Can only view and download documents
- **Processed Documents Only**: Access only to approved, processed documents
- **No Original Access**: Cannot access original uploaded documents
- **No Processing Access**: Cannot upload or process documents

### **Restrictions**
- ❌ **Cannot Upload Documents**: No document upload capabilities
- ❌ **Cannot Process Documents**: No document processing access
- ❌ **Cannot Manage Templates**: No template management access
- ❌ **Cannot Manage Users**: No user management capabilities
- ❌ **Cannot Access Originals**: No access to original uploaded files

---

## 🔐 **Security Implementation**

### **JWT Token Claims**
Each user's JWT token contains:
```json
{
  "sub": "123",           // User ID
  "email": "user@example.com",
  "role": "Admin|Vendor|Client",
  "iat": 1234567890,
  "exp": 1234567890
}
```

### **Authorization Attributes**
- `[Authorize]` - Requires any authenticated user
- `[Authorize(Roles = "Admin")]` - Admin only
- `[Authorize(Roles = "Vendor")]` - Vendor only
- `[Authorize(Roles = "Client,Admin")]` - Client or Admin

### **Data Isolation**
- **Vendor Data Isolation**: Vendors can only access their own documents
- **Client Data Filtering**: Clients can only access processed, approved documents
- **Admin Override**: Admins can access all data across the system

---

## 📊 **Role Comparison Matrix**

| Feature | Admin | Vendor | Client |
|---------|-------|--------|--------|
| **User Management** | ✅ Full | ❌ None | ❌ None |
| **Template Management** | ✅ Full | ❌ None | ❌ None |
| **Document Upload** | ✅ Yes | ✅ Yes | ❌ No |
| **Document Processing** | ✅ Yes | ✅ Yes | ❌ No |
| **Document Download** | ✅ All | ❌ Own Only | ✅ Processed Only |
| **Data Extraction** | ✅ All | ❌ Own Only | ✅ Processed Only |
| **System Configuration** | ✅ Full | ❌ None | ❌ None |
| **Audit Access** | ✅ Full | ❌ Own Only | ❌ None |

---

## 🚀 **Getting Started**

### **For Admins**
1. Login with admin credentials
2. Create vendor accounts via `/api/auth/register-vendor`
3. Define templates via `/api/templates`
4. Monitor system activity

### **For Vendors**
1. Login with vendor credentials
2. Upload PDF documents via `/api/documents/upload`
3. Select appropriate template
4. Preview and confirm processed documents

### **For Clients**
1. Login with client credentials
2. Browse processed documents via `/api/documents/processed`
3. Download documents via `/api/documents/processed/{id}/file`
4. Extract structured data for integration

---

## 🔄 **Role Transitions**

### **Promoting Users**
- **Vendor → Admin**: Requires system administrator intervention
- **Client → Vendor**: Requires admin approval and account modification
- **Client → Admin**: Requires system administrator intervention

### **Account Management**
- Only admins can create new vendor accounts
- Role changes require admin privileges
- Account deactivation can be performed by admins
