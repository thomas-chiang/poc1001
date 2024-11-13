using System;

namespace UseCase1004
{
    public class Investigate1004
    {
        private string company;
        private string formNo;

        public Investigate1004(string company, string formNo)
        {
            this.company = company;
            this.formNo = formNo;
        }

        public string Company
        {
            get { return company; }
        }

        public string FormNo
        {
            get { return formNo; }
        }
    }
}
