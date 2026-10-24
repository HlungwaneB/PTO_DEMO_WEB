using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PTO_Management_System.Models
{
    /// <summary>Same custom exception filter as the CUT project: shows Views/Shared/CustomError.cshtml.</summary>
    public class CustomExceptionFilter : IExceptionFilter
    {
        private readonly IModelMetadataProvider _modelMetadataProvider;

        public CustomExceptionFilter(IModelMetadataProvider modelMetadataProvider)
        {
            _modelMetadataProvider = modelMetadataProvider;
        }

        public void OnException(ExceptionContext context)
        {
            var result = new ViewResult { ViewName = "CustomError" };
            result.ViewData = new ViewDataDictionary(_modelMetadataProvider, context.ModelState);
            result.ViewData.Add("Exception", context.Exception);

            context.ExceptionHandled = true; // mark exception as handled
            context.Result = result;
        }
    }
}
