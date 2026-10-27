package __RUNTIME__;

import java.net.URI;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ProblemDetail;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.servlet.mvc.method.annotation.ResponseEntityExceptionHandler;

@RestControllerAdvice
public class ProblemDetailsAdvice extends ResponseEntityExceptionHandler {
    private static final String PREFIX = "urn:cratis:stage-axon:problem:";

    @ExceptionHandler(CommandValidationException.class)
    public ProblemDetail validation(CommandValidationException exception) {
        var problem = problem(HttpStatus.BAD_REQUEST, "validation", exception.getMessage());
        problem.setProperty("errors", exception.errors());
        return problem;
    }

    @ExceptionHandler(UniqueValueConflict.class)
    public ProblemDetail conflict(UniqueValueConflict exception) {
        return problem(HttpStatus.CONFLICT, "conflict", exception.getMessage());
    }

    @ExceptionHandler(IllegalArgumentException.class)
    public ProblemDetail invalid(IllegalArgumentException exception) {
        return problem(HttpStatus.BAD_REQUEST, "invalid-argument", exception.getMessage());
    }

    @ExceptionHandler(UnsupportedOperationException.class)
    public ProblemDetail unsupported(UnsupportedOperationException exception) {
        return problem(HttpStatus.NOT_IMPLEMENTED, "unsupported", exception.getMessage());
    }

    @ExceptionHandler(QueryCardinalityException.class)
    public ProblemDetail cardinality(QueryCardinalityException exception) {
        var missing = exception.count() == 0;
        var problem = problem(missing ? HttpStatus.NOT_FOUND : HttpStatus.INTERNAL_SERVER_ERROR, missing ? "query-result-missing" : "query-cardinality", exception.getMessage());
        problem.setProperty("cardinality", exception.cardinality());
        problem.setProperty("count", exception.count());
        return problem;
    }

    @ExceptionHandler(LiveCapacityException.class)
    public ResponseEntity<ProblemDetail> liveCapacity(LiveCapacityException exception) {
        return ResponseEntity.status(HttpStatus.SERVICE_UNAVAILABLE)
            .header("Retry-After", "5")
            .contentType(MediaType.APPLICATION_PROBLEM_JSON)
            .body(problem(HttpStatus.SERVICE_UNAVAILABLE, "live-capacity", exception.getMessage()));
    }

    private static ProblemDetail problem(HttpStatus status, String type, String detail) {
        var problem = ProblemDetail.forStatusAndDetail(status, detail);
        problem.setType(URI.create(PREFIX + type));
        return problem;
    }
}
