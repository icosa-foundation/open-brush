/*
 * Icosa API
 *
 * No description provided (generated by Openapi Generator https://github.com/openapitools/openapi-generator)
 *
 * The version of the OpenAPI document: 0.1.0
 * Generated by: https://github.com/openapitools/openapi-generator.git
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using OpenAPIDateConverter = Org.OpenAPITools.Client.OpenAPIDateConverter;

namespace Org.OpenAPITools.Model
{
    /// <summary>
    /// EmailChangeAuthenticated
    /// </summary>
    [DataContract(Name = "EmailChangeAuthenticated")]
    public partial class EmailChangeAuthenticated : IEquatable<EmailChangeAuthenticated>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EmailChangeAuthenticated" /> class.
        /// </summary>
        [JsonConstructorAttribute]
        protected EmailChangeAuthenticated() { }
        /// <summary>
        /// Initializes a new instance of the <see cref="EmailChangeAuthenticated" /> class.
        /// </summary>
        /// <param name="newEmail">newEmail (required).</param>
        /// <param name="currentPassword">currentPassword (required).</param>
        public EmailChangeAuthenticated(string newEmail = default(string), string currentPassword = default(string))
        {
            // to ensure "newEmail" is required (not null)
            if (newEmail == null)
            {
                throw new ArgumentNullException("newEmail is a required property for EmailChangeAuthenticated and cannot be null");
            }
            this.NewEmail = newEmail;
            // to ensure "currentPassword" is required (not null)
            if (currentPassword == null)
            {
                throw new ArgumentNullException("currentPassword is a required property for EmailChangeAuthenticated and cannot be null");
            }
            this.CurrentPassword = currentPassword;
        }

        /// <summary>
        /// Gets or Sets NewEmail
        /// </summary>
        [DataMember(Name = "newEmail", IsRequired = true, EmitDefaultValue = true)]
        public string NewEmail { get; set; }

        /// <summary>
        /// Gets or Sets CurrentPassword
        /// </summary>
        [DataMember(Name = "currentPassword", IsRequired = true, EmitDefaultValue = true)]
        public string CurrentPassword { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("class EmailChangeAuthenticated {\n");
            sb.Append("  NewEmail: ").Append(NewEmail).Append("\n");
            sb.Append("  CurrentPassword: ").Append(CurrentPassword).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public virtual string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        /// <param name="input">Object to be compared</param>
        /// <returns>Boolean</returns>
        public override bool Equals(object input)
        {
            return this.Equals(input as EmailChangeAuthenticated);
        }

        /// <summary>
        /// Returns true if EmailChangeAuthenticated instances are equal
        /// </summary>
        /// <param name="input">Instance of EmailChangeAuthenticated to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(EmailChangeAuthenticated input)
        {
            if (input == null)
            {
                return false;
            }
            return 
                (
                    this.NewEmail == input.NewEmail ||
                    (this.NewEmail != null &&
                    this.NewEmail.Equals(input.NewEmail))
                ) && 
                (
                    this.CurrentPassword == input.CurrentPassword ||
                    (this.CurrentPassword != null &&
                    this.CurrentPassword.Equals(input.CurrentPassword))
                );
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hashCode = 41;
                if (this.NewEmail != null)
                {
                    hashCode = (hashCode * 59) + this.NewEmail.GetHashCode();
                }
                if (this.CurrentPassword != null)
                {
                    hashCode = (hashCode * 59) + this.CurrentPassword.GetHashCode();
                }
                return hashCode;
            }
        }

    }

}
