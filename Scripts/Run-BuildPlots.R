#
# Run-BuildPlots.R
#
# Author: Denes Solti
#

args <- commandArgs(trailingOnly = TRUE)
if (length(args) != 1) {
  stop("Usage: Rscript Scripts/Run-BuildPlots.R <path-to-BuildPlots.R>")
}

buildPlotsPath <- normalizePath(args[1], mustWork = TRUE)
buildPlots <- readLines(buildPlotsPath, warn = FALSE)

buildPlots <- sub(
  'group_by_(.dots = c("Target_Method", "Job_Id"))',
  'group_by(Target_Method, Job_Id)',
  buildPlots,
  fixed = TRUE
)
buildPlots <- sub(
  'geom_bar(position=position_dodge(), stat="identity")',
  'geom_bar(position=position_dodge(), stat="identity") +
    theme(axis.text.x = element_text(angle = 45, hjust = 1))',
  buildPlots,
  fixed = TRUE
)
buildPlots <- gsub(
  r"(guides(fill=guide_legend(title="Job")))",
  r"(guides(fill=guide_legend(title="Job")) +
    scale_fill_discrete(labels = function(label) {
      label <- sub("ScenarioKind=", "", sub("^DefaultJob ", "", label), fixed = TRUE)
      sub("&RouteMatcherFactory=(.*)$", ' (\\1)', label)
    }))",
  buildPlots,
  fixed = TRUE
)

writeLines(buildPlots, buildPlotsPath)
setwd(dirname(buildPlotsPath))

rscript <- file.path(R.home("bin"), if (.Platform$OS.type == "windows") "Rscript.exe" else "Rscript")
quit(status = system2(rscript, buildPlotsPath))
