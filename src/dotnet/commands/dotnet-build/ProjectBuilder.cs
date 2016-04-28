// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Files;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Build
{
    internal abstract class ProjectBuilder
    {
        private readonly bool _skipDependencies;

        public ProjectBuilder(bool skipDependencies)
        {
            _skipDependencies = skipDependencies;
        }

        private Dictionary<ProjectContextIdentity, CompilationResult> _compilationResults = new Dictionary<ProjectContextIdentity, CompilationResult>();

        public IEnumerable<CompilationResult> Build(IEnumerable<ProjectGraphNode> roots)
        {
            foreach (var projectNode in roots)
            {
                yield return Build(projectNode);
            }
        }

        public CompilationResult? GetCompilationResult(ProjectGraphNode projectNode)
        {
            CompilationResult result;
            if (_compilationResults.TryGetValue(projectNode.ProjectContext.Identity, out result))
            {
                return result;
            }
            return null;
        }

        protected virtual bool NeedsRebuilding(ProjectGraphNode projectNode)
        {
            return true;
        }

        protected virtual void ProjectSkiped(ProjectGraphNode projectNode)
        {
        }

        protected abstract CompilationResult RunCompile(ProjectGraphNode projectNode);

        private CompilationResult Build(ProjectGraphNode projectNode)
        {
            CompilationResult result;
            if (_compilationResults.TryGetValue(projectNode.ProjectContext.Identity, out result))
            {
                return result;
            }
            result = CompileWithDependencies(projectNode);

            _compilationResults[projectNode.ProjectContext.Identity] = result;

            return result;
        }

        private CompilationResult CompileWithDependencies(ProjectGraphNode projectNode)
        {
            if (!_skipDependencies)
            {
                foreach (var dependency in projectNode.Dependencies)
                {
                    var result = Build(dependency);
                    if (result == CompilationResult.Failure)
                    {
                        return CompilationResult.Failure;
                    }
                }
            }

            var context = projectNode.ProjectContext;
            if (!HasSourceFiles(context))
            {
                return CompilationResult.IncrementalSkip;
            }

            if (NeedsRebuilding(projectNode))
            {
                return RunCompile(projectNode);
            }
            else
            {
                ProjectSkiped(projectNode);
                return CompilationResult.IncrementalSkip;
            }
        }

        private static bool HasSourceFiles(ProjectContext context)
        {
            var compilerOptions = context.ProjectFile.GetCompilerOptions(context.TargetFramework, null);

            if (compilerOptions.CompileInclude == null)
            {
                return context.ProjectFile.Files.SourceFiles.Any();
            }

            var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilerOptions.CompileInclude, "/", diagnostics: null);

            return includeFiles.Any();
        }
    }
}