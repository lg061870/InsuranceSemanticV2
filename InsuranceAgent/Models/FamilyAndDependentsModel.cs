using System;
using System.Collections.Generic;

namespace InsuranceAgent.Models {
    public class FamilyAndDependentsModel
    {
        public string MaritalStatus { get; set; } = string.Empty;
        public bool Dependents { get; set; }
        public List<string> DependentAges { get; set; } = new List<string>();
    }
}