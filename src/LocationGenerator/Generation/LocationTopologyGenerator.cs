using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Services.Binds;
using LocationGenerator.Contracts;
using LocationGenerator.Params;
using LocationGenerator.Strategies;

namespace LocationGenerator.Generation
{
    /// <summary>
    /// Generates and commits location topologies using registered strategies.
    /// </summary>
    /// <seealso cref="ILocationTopologyStrategy"/>
    public sealed class LocationTopologyGenerator : IChainGenerator
    {
        private readonly LocationTopologyRequest _request;
        private readonly LocationStrategyRegistry _registry;
        private readonly ILocationRandom _random;
        private readonly LocationGraphInspector _inspector = new LocationGraphInspector();
        private readonly LocationTopologyPlanValidator _validator = new LocationTopologyPlanValidator();
        private readonly LocationPlanCommitter _committer = new LocationPlanCommitter();

        /// <summary>
        /// Initializes a new instance of <see cref="LocationTopologyGenerator"/>.
        /// </summary>
        /// <param name="request">The topology generation request.</param>
        /// <param name="registry">The registry value.</param>
        /// <param name="random">The random source used to make deterministic choices.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public LocationTopologyGenerator(LocationTopologyRequest request,
            LocationStrategyRegistry registry, ILocationRandom random)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Generates and commits the requested location topology.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="root">The root location for the operation.</param>
        /// <param name="request">The topology generation request.</param>
        /// <returns>The committed topology result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        /// <exception cref="LocationGenerationValidationException">Thrown when the request, graph, strategy result, or mutation plan is invalid.</exception>
        public LocationTopologyResult Generate(Plot plot, IElement root, LocationTopologyRequest request)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (root == null) throw new ArgumentNullException(nameof(root));

            _inspector.ValidateRoot(plot, root);
            var strategy = ValidateRequest(request);
            var scope = _inspector.Inspect(plot, root, request.Mode);
            ValidateAttachment(request, scope);

            var newLocations = Enumerable.Range(0, request.NodeCount)
                .Select(_ => (IElement)new Element(ElemType.Location))
                .ToArray();
            var context = new LocationStrategyContext(scope.ExistingGraph,
                newLocations.Select(location => location.Id).ToArray(), request.AttachmentId, _random);
            var proposal = strategy.Build(context, request.Options);

            var plan = new LocationMutationPlan();
            plan.RemoveScope(scope);
            foreach (var location in newLocations)
            {
                plan.AddLocation(location);
                plan.AddRelation(new Relation(scope.Root, location, BaseRelationKeys.Contains, true));
            }

            _validator.Validate(plot, scope, request, context, proposal, strategy, plan);
            _committer.Commit(plot, plan);

            IElement? attachment = null;
            if (proposal.AttachmentId.HasValue)
                attachment = scope.ExistingLocations[proposal.AttachmentId.Value];
            return new LocationTopologyResult(scope.Root, newLocations, attachment,
                scope.RemovedLocations.Select(location => location.Id).ToArray());
        }

        /// <summary>
        /// Generates a backward-compatible linear location chain.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="preparedElement">The prepared location element to populate.</param>
        /// <param name="recursion">The recursion value.</param>
        /// <returns>The resulting backward-compatible linear location chain.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        /// <exception cref="LocationGenerationValidationException">Thrown when recursion is negative or topology generation fails validation.</exception>
        public IElement GenerateChain(Plot plot, IElement preparedElement, int recursion = 3)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (preparedElement == null) throw new ArgumentNullException(nameof(preparedElement));
            if (recursion < 0)
                throw Validation("recursion.invalid", "recursion", "Recursion must not be negative.");

            _inspector.ValidateRoot(plot, preparedElement);
            if (recursion == 0) return preparedElement;

            Generate(plot, preparedElement, _request);
            return preparedElement;
        }

        /// <summary>
        /// Asynchronously generates a backward-compatible linear location chain.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="preparedElement">The prepared location element to populate.</param>
        /// <param name="recursion">The recursion value.</param>
        /// <returns>A task that represents the asynchronous operation and contains its result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        /// <exception cref="LocationGenerationValidationException">Thrown when recursion is negative or topology generation fails validation.</exception>
        /// <seealso cref="GenerateChain"/>
        public async Task<IElement> GenerateChainAsync(Plot plot, IElement preparedElement, int recursion = 3)
        {
            await Task.CompletedTask;
            return GenerateChain(plot, preparedElement, recursion);
        }

        /// <summary>
        /// Validates request.
        /// </summary>
        /// <param name="request">The topology generation request.</param>
        /// <returns>The registered strategy selected by the request.</returns>
        /// <exception cref="LocationGenerationValidationException">Thrown when the location-generation request or result is invalid.</exception>
        private ILocationTopologyStrategy ValidateRequest(LocationTopologyRequest? request)
        {
            if (request == null)
                throw Validation("request.required", "request", "A topology request is required.");

            var diagnostics = new List<LocationGenerationDiagnostic>();
            if (request.NodeCount <= 0)
                diagnostics.Add(new LocationGenerationDiagnostic("nodeCount.invalid", "nodeCount",
                    "NodeCount must be greater than zero."));
            if (!Enum.IsDefined(typeof(LocationGenerationMode), request.Mode))
                diagnostics.Add(new LocationGenerationDiagnostic("mode.invalid", "mode",
                    "The generation mode is not supported."));
            if (request.Mode == LocationGenerationMode.Overwrite && request.AttachmentId.HasValue)
                diagnostics.Add(new LocationGenerationDiagnostic("attachment.unavailable", "attachmentId",
                    "Overwrite does not accept an attachment.", new[] { request.AttachmentId.Value }));
            if (diagnostics.Count > 0)
                throw new LocationGenerationValidationException(diagnostics);

            return _registry.Resolve(request.StrategyId, request.Options);
        }

        /// <summary>
        /// Validates attachment.
        /// </summary>
        /// <param name="request">The topology generation request.</param>
        /// <param name="scope">The scope value.</param>
        /// <exception cref="LocationGenerationValidationException">Thrown when the requested attachment is unavailable.</exception>
        private static void ValidateAttachment(LocationTopologyRequest request, LocationGenerationScope scope)
        {
            if (!request.AttachmentId.HasValue || scope.ExistingGraph.NodeIds.Count > 0) return;
            throw Validation("attachment.unavailable", "attachmentId",
                "An attachment is unavailable when the root has no existing local graph.",
                request.AttachmentId.Value);
        }

        /// <summary>
        /// Creates a validation exception containing the supplied diagnostic.
        /// </summary>
        /// <param name="code">The code value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="message">The message value.</param>
        /// <param name="ids">The ids value.</param>
        /// <returns>A validation exception containing the supplied diagnostic.</returns>
        private static LocationGenerationValidationException Validation(string code, string path,
            string message, params Guid[] ids) =>
            new LocationGenerationValidationException(new[]
            {
                new LocationGenerationDiagnostic(code, path, message, ids)
            });
    }
}
